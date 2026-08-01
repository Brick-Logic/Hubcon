using System.Collections.Concurrent;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;
using System.Reflection;
using System.Threading.RateLimiting;

#pragma warning disable CS1591
namespace Hubcon.Server.Core.RateLimiting
{
    public class GlobalRateLimiterManager(
        IOperationCache cache,
        IInternalServerOptions options,
        IOperationConfigRegistry operationConfigRegistry,
        IOperationRegistry operationRegistry) : IGlobalRateLimiterManager
    {
        private readonly RateLimiter globalRateLimiter = new TokenBucketRateLimiter(options.GlobalRateLimiterOptions);

        private readonly SettingsManager settingsManager =
            new SettingsManager(operationRegistry, operationConfigRegistry);

        public async ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type,
            HubconTransportAttribute transport, IOperationRequest? operation = null, int permits = 1,
            CancellationToken cancellationToken = default)
        {
            if (options.ThrottlingIsDisabled) return true;

            try
            {
                if (!(await globalRateLimiter.AcquireAsync(permits, cancellationToken)).IsAcquired)
                {
                    return false;
                }

                var typeLimiter = GetOrCreateLimiter(anchorKey, type, transport);
                if (typeLimiter != null && !(await typeLimiter.AcquireAsync(permits, cancellationToken)).IsAcquired)
                {
                    return false;
                }

                if (operation != null)
                {
                    var contractLimiter = GetOrCreateContractLimiter(anchorKey, operation, transport);
                    if (contractLimiter != null &&
                        !(await contractLimiter.AcquireAsync(permits, cancellationToken)).IsAcquired)
                    {
                        return false;
                    }

                    var opLimiter = GetOrCreateOperationLimiter(anchorKey, operation, transport);
                    if (opLimiter != null && !(await opLimiter.AcquireAsync(permits, cancellationToken)).IsAcquired)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, Guid resourceId, HubconTransportAttribute transport, int permits = 1, CancellationToken cancellationToken = default)
        {
            if (options.ThrottlingIsDisabled)
                return true;

            try
            {
                // 1. Capa Global
                await globalRateLimiter.AcquireAsync(permits, cancellationToken);

                // 2. Capa por Tipo de Mensaje
                var typeLimiter = GetOrCreateLimiter(anchorKey, type, transport);
                if (typeLimiter != null) await typeLimiter.AcquireAsync(permits, cancellationToken);

                // 3. Capa vinculada por Guid (Link/Unlink)
                if (resourceId != Guid.Empty)
                {
                    string linkKey = $"link_{anchorKey}_{resourceId}";

                    // Intentamos obtener el request que guardamos en el Link
                    if (cache.TryGetValue(linkKey, out IOperationRequest? request) && request != null)
                    {
                        // Recuperamos el bucket específico de este Guid a través del settingsManager
                        var settings = GetLinkedSettings(type, resourceId);

                        if (settings?.RateBucket != null)
                        {
                            await settings.RateBucket.AcquireAsync(permits, cancellationToken);
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private RateLimiter? GetOrCreateLimiter(string anchorKey, MessageType type, HubconTransportAttribute transport)
        {
            string key = $"limiter_{anchorKey}_{GetGroupKey(type)}";

            return cache.GetOrCreate(key, () => { return CreateLimiterByMessageType(type, transport); });
        }

        private RateLimiter? GetOrCreateContractLimiter(string anchorKey,
            IOperationEndpoint endpoint,
            HubconTransportAttribute transport)
        {
            if (operationRegistry.TryGetOperationBlueprint(endpoint, transport, out var blueprint))
            {
                string key = $"op_{anchorKey}:{blueprint!.SimpleContractName}";

                return cache.GetOrCreate(key, () =>
                {
                    var settings = blueprint.ContractType.GetCustomAttribute<RateLimitAttribute>() ??
                                   blueprint.ControllerType.GetCustomAttribute<RateLimitAttribute>();
                    return settings?.RateBucket;
                });
            }

            return null;
        }

        private RateLimiter? GetOrCreateOperationLimiter(string anchorKey, IOperationEndpoint endpoint,
            HubconTransportAttribute transport)
        {
            if (operationRegistry.TryGetOperationBlueprint(endpoint, transport, out var blueprint))
            {
                string key = $"op_{anchorKey}:{blueprint!.SimpleContractName}:{blueprint.OperationName}";

                return cache.GetOrCreate(key, () =>
                {
                    var settings = settingsManager.GetSettings<RateLimitAttribute>(endpoint, transport, () => null!);
                    return settings?.RateBucket;
                });
            }

            return null;
        }

        private string GetGroupKey(MessageType type) => type switch
        {
            MessageType.subscription_init or MessageType.subscription_data => "sub",
            MessageType.stream_init or MessageType.stream_data => "stream",
            MessageType.ingest_init or MessageType.ingest_data => "ingest",
            _ => type.ToString()
        };

        private RateLimiter? CreateLimiterByMessageType(MessageType type, HubconTransportAttribute transport)
        {
            if (!options.TransportSettings.TryGetValue(transport, out var settings))
                settings = transport.DefaultTransportSettings;
            
            var bucketOptions = type switch
            {
                MessageType.connection_ack
                    or MessageType.connection_init
                    or MessageType.pong
                    or MessageType.error
                    or MessageType.ack
                    or MessageType.ingest_init_ack
                    or MessageType.ingest_data_ack
                    or MessageType.operation_response
                    => null,

                MessageType.ping
                    => settings.PingOperationLimiterOptions,

                MessageType.operation_invoke
                    => settings.InvokeOperationLimiterOptions,

                MessageType.operation_call
                    => settings.CallOperationLimiterOptions,

                MessageType.stream_init
                    or MessageType.stream_complete
                    or MessageType.stream_data
                    or MessageType.stream_data_ack
                    or MessageType.stream_data_with_ack
                    => settings.StreamOperationLimiterOptions,

                MessageType.ingest_init
                    or MessageType.ingest_data
                    or MessageType.ingest_data_with_ack
                    or MessageType.ingest_complete
                    or MessageType.ingest_result
                    => settings.IngestOperationLimiterOptions,

                MessageType.token_update
                    => settings.ControlMessagesRateLimiterOptions,

                _ => null,
            };

            return bucketOptions != null ? new TokenBucketRateLimiter(bucketOptions) : null;
        }

        public ValueTask Link(string anchorKey, Guid id, HubconTransportAttribute transportAttribute,
            IOperationRequest request)
        {
            if (!operationRegistry.TryGetOperationBlueprint(request, transportAttribute, out var blueprint))
            {
                return ValueTask.CompletedTask;
            }
            
            operationConfigRegistry.Link(id, blueprint!);
            
            string linkKey = $"link_{anchorKey}_{id}";

            cache.Set(linkKey, request, () => { operationConfigRegistry.Unlink(id); });

            return ValueTask.CompletedTask;
        }

        public ValueTask Unlink(string anchorKey, Guid operationId)
        {
            operationConfigRegistry.Unlink(operationId);

            string linkKey = $"link_{anchorKey}_{operationId}";
            cache.Remove(linkKey);

            return ValueTask.CompletedTask;
        }

        private RateLimitAttribute? GetLinkedSettings(MessageType type, Guid id)
        {
            return type switch
            {
                MessageType.connection_ack
                    or MessageType.connection_init
                    or MessageType.pong
                    or MessageType.error
                    or MessageType.ack
                    or MessageType.ingest_init_ack
                    or MessageType.ingest_data_ack
                    or MessageType.operation_response
                    => null,

                // Ping limiter (para evitar abuso)
                MessageType.ping
                    => null,

                // Operation messages (round-trip)
                MessageType.operation_invoke
                    => settingsManager.GetSettings(id, () => new RateLimitAttribute()),

                // Operation call (fire and forget)
                MessageType.operation_call
                    => settingsManager.GetSettings(id, () => new RateLimitAttribute()),

                // Subscription group (comparten el mismo limiter)
                MessageType.subscription_init
                    or MessageType.subscription_data
                    or MessageType.subscription_data_with_ack
                    or MessageType.subscription_complete
                    => settingsManager.GetSettings(id, () => new RateLimitAttribute()),

                // Stream group (todos comparten)
                MessageType.stream_init
                    or MessageType.stream_complete
                    or MessageType.stream_data
                    or MessageType.stream_data_ack
                    or MessageType.stream_data_with_ack
                    => settingsManager.GetSettings(id, () => new RateLimitAttribute()),

                // Ingest group (comparten)
                MessageType.ingest_init
                    or MessageType.ingest_data
                    or MessageType.ingest_data_with_ack
                    or MessageType.ingest_complete
                    or MessageType.ingest_result
                    => settingsManager.GetSettings(id, () => new RateLimitAttribute()),

                _ => null,
            };
        }
    }
}