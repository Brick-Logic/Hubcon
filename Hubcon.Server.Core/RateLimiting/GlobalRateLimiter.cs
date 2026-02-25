using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Core.RateLimiting
{
    public class GlobalRateLimiterManager(
        IMemoryCache cache,
        IInternalServerOptions options,
        ISettingsManager settingsManager,
        IOperationConfigRegistry operationConfigRegistry,
        IOperationRegistry operationRegistry) : IGlobalRateLimiterManager
    {
        // Las opciones de expiración: si un cliente no envía mensajes por 10 min, liberamos sus limiters
        private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10))
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                if (value is IDisposable disposable) disposable.Dispose();
            });

        private readonly RateLimiter globalRateLimiter = new TokenBucketRateLimiter(options.GlobalRateLimiterOptions);

        public async ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, HubconTransportAttribute transport, IOperationRequest? operation = null, CancellationToken cancellationToken = default)
        {
            if (options.ThrottlingIsDisabled) return true;

            try
            {
                // 1. Capa Global (Configurada en el Singleton)
                // Podrías tener un limiter estático aquí o sacarlo de las opciones
                await globalRateLimiter.AcquireAsync(1, cancellationToken);

                // 2. Capa por Tipo de Mensaje (Websocket, Stream, Ingest, etc.)
                var typeLimiter = GetOrCreateLimiter(anchorKey, type);
                if(typeLimiter != null) await typeLimiter.AcquireAsync(1, cancellationToken);

                if (operation != null)
                {
                    var contractLimiter = GetOrCreateContractLimiter(anchorKey, operation, transport);
                    if (contractLimiter != null)
                        await contractLimiter.AcquireAsync(1, cancellationToken);

                    var opLimiter = GetOrCreateOperationLimiter(anchorKey, operation, transport);
                    if (opLimiter != null)
                        await opLimiter.AcquireAsync(1, cancellationToken);
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, Guid resourceId, CancellationToken cancellationToken = default)
        {
            if (options.ThrottlingIsDisabled) return true;

            try
            {
                // 1. Capa Global
                await globalRateLimiter.AcquireAsync(1, cancellationToken);

                // 2. Capa por Tipo de Mensaje
                var typeLimiter = GetOrCreateLimiter(anchorKey, type);
                if (typeLimiter != null) await typeLimiter.AcquireAsync(1, cancellationToken);

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
                            await settings.RateBucket.AcquireAsync(1, cancellationToken);
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

        private RateLimiter? GetOrCreateLimiter(string anchorKey, MessageType type)
        {
            string key = $"limiter_{anchorKey}_{GetGroupKey(type)}";

            return cache.GetOrCreate(key, entry =>
            {
                entry.SetOptions(_cacheOptions);
                return CreateLimiterByMessageType(type);
            });
        }

        private RateLimiter? GetOrCreateContractLimiter(string anchorKey, IOperationEndpoint endpoint, HubconTransportAttribute transport)
        {
            if (operationRegistry.TryGetOperationBlueprint(endpoint, transport, out var blueprint))
            {
                string key = $"op_{anchorKey}:{blueprint!.SimpleContractName}";

                return cache.GetOrCreate(key, entry =>
                {
                    entry.SetOptions(_cacheOptions);
                    var settings = settingsManager.GetSettings(endpoint, transport, () => new RateLimitAttribute());
                    return settings.RateBucket;
                });
            }

            return null;
        }

        private RateLimiter? GetOrCreateOperationLimiter(string anchorKey, IOperationEndpoint endpoint, HubconTransportAttribute transport)
        {
            if (operationRegistry.TryGetOperationBlueprint(endpoint, transport, out var blueprint))
            {
                string key = $"op_{anchorKey}:{blueprint!.SimpleContractName}:{blueprint.OperationName}";

                return cache.GetOrCreate(key, entry =>
                {
                    entry.SetOptions(_cacheOptions);
                    var settings = settingsManager.GetSettings(endpoint, transport, () => new RateLimitAttribute());
                    return settings.RateBucket;
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

        private RateLimiter? CreateLimiterByMessageType(MessageType type)
        {
            // Aquí usas la lógica de tu switch original pero instanciando nuevos limiters
            // que serán cacheados por el MemoryCache
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

                // Ping limiter (para evitar abuso)
                MessageType.ping 
                    => options.WebsocketPingRateLimiter.Invoke(),

                // Operation messages (round-trip)
                MessageType.operation_invoke
                    => options.WebsocketRoundTripMethodRateLimiter.Invoke(),

                // Operation call (fire and forget)
                MessageType.operation_call
                    => options.WebsocketRoundTripMethodRateLimiter.Invoke(),

                // Subscription group (comparten el mismo limiter)
                MessageType.subscription_init
                or MessageType.subscription_data
                or MessageType.subscription_data_with_ack
                or MessageType.subscription_complete
                    => options.WebsocketSubscriptionRateLimiter.Invoke(),

                // Stream group (todos comparten)
                MessageType.stream_init
                or MessageType.stream_complete
                or MessageType.stream_data
                or MessageType.stream_data_ack
                or MessageType.stream_data_with_ack
                    => options.WebsocketStreamingRateLimiter.Invoke(),

                // Ingest group (comparten)
                MessageType.ingest_init
                or MessageType.ingest_data
                or MessageType.ingest_data_with_ack
                or MessageType.ingest_complete
                or MessageType.ingest_result
                    => options.WebsocketRoundTripMethodRateLimiter.Invoke(),

                MessageType.token_update 
                    => options.WebsocketTokenUpdateRateLimiter.Invoke(),

                _ => null,
            };

            return bucketOptions != null ? new TokenBucketRateLimiter(bucketOptions) : null;
        }

        public ValueTask LinkLimiter(string anchorKey, Guid id, HubconTransportAttribute transportAttribute, IOperationRequest request)
        {
            // 1. Obtenemos el blueprint como hacías antes
            if (!operationRegistry.TryGetOperationBlueprint(request, transportAttribute, out var blueprint))
            {
                return ValueTask.CompletedTask;
            }

            // 2. Registramos en el config registry (asumo que es un singleton externo)
            // Nota: Si 'id' viene del request o es un Guid nuevo, asegúrate de pasarlo por parámetro
            operationConfigRegistry.Link(id, blueprint!);

            // 3. Persistimos la relación en el MemoryCache con expiración
            // Esto evita que si el cliente desaparece, la entrada quede "viva" para siempre
            string linkKey = $"link_{anchorKey}_{id}";

            cache.Set(linkKey, request, new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30)) // Tiempo de gracia
                .RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    // Limpieza automática en cascada si expira por inactividad
                    operationConfigRegistry.Unlink(id);
                }));

            return ValueTask.CompletedTask;
        }

        public ValueTask UnlinkLimiter(string anchorKey, Guid operationId)
        {
            // 1. Desvinculamos del registro de configuración
            operationConfigRegistry.Unlink(operationId);

            // 2. Removemos explícitamente del cache
            string linkKey = $"link_{anchorKey}_{operationId}";
            cache.Remove(linkKey);

            return ValueTask.CompletedTask;
        }

        private RateLimitAttribute? GetLinkedSettings(MessageType type, Guid id)
        {
            // No limiters (inicialización, ack, errores, pong, etc.)
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
