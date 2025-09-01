using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Core.RateLimiting
{
    public class RateLimiterManager(ISettingsManager settingsManager, IInternalServerOptions options) : IRateLimiterManager, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<MessageType, RateLimiter> _typeLimiters = new();

        private readonly ConcurrentDictionary<IOperationEndpoint, HubconSettings> operationSettings = new();
        private readonly ConcurrentDictionary<Guid, IOperationEndpoint> linkedSettings = new();

        private readonly RateLimiter? _globalLimiter = new TokenBucketRateLimiter(options.WebsocketReaderRateLimiter.Invoke());
        private RateLimiter? _ingestLimiter = null;
        private RateLimiter? _streamLimiter = null;
        private RateLimiter? _subscriptionLimiter = null;
        private RateLimiter? _operationCallLimiter = null;
        private RateLimiter? _operationInvokeLimiter = null;

        public async ValueTask<bool> TryAcquireAsync(MessageType type, IOperationEndpoint? operation = null)
        {
            try
            {
                if (_globalLimiter is not null)
                    await _globalLimiter.AcquireAsync();

                if (_typeLimiters.TryGetValue(type, out var typeLimiter))
                {
                    await typeLimiter.AcquireAsync();
                }
                else
                {
                    var limiter = GetLimiterForMessageType(type);
                    if (limiter is not null)
                    {
                        _typeLimiters[type] = limiter;
                        await limiter.AcquireAsync();
                    }
                }

                if (operation is not null)
                {
                    var settings = operationSettings.GetOrAdd(operation, x => GetOperationSettings(type, operation)!);
                    await settings.RateBucket.AcquireAsync();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask<bool> TryAcquireAsync(MessageType type, Guid messageId)
        {
            try
            {
                if (_globalLimiter is not null)
                    await _globalLimiter.AcquireAsync();

                if (_typeLimiters.TryGetValue(type, out var typeLimiter))
                {
                    await typeLimiter.AcquireAsync();
                }
                else
                {
                    var limiter = GetLimiterForMessageType(type);
                    if (limiter is not null)
                    {
                        _typeLimiters[type] = limiter;
                        await limiter.AcquireAsync();
                    }
                }

                if (messageId != Guid.Empty)
                {
                    linkedSettings.TryGetValue(messageId, out IOperationEndpoint? operationEndpoint);
                    
                    if (operationEndpoint != null)
                    {
                        var settings = operationSettings.GetOrAdd(operationEndpoint, x => GetLinkedSettings(type, messageId)!);
                        await settings.RateBucket.AcquireAsync();
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public ValueTask Link(Guid id, IOperationEndpoint endpoint)
        {
            linkedSettings.TryAdd(id, endpoint);
            return ValueTask.CompletedTask;
        }

        public ValueTask Unlink(Guid id)
        {
            linkedSettings.TryRemove(id, out _);
            return ValueTask.CompletedTask;
        }

        private RateLimiter? GetLimiterForMessageType(MessageType type)
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
                    => new TokenBucketRateLimiter(options.WebsocketPingRateLimiter.Invoke()),

                // Operation messages (round-trip)
                MessageType.operation_invoke 
                    => _operationInvokeLimiter ??= new TokenBucketRateLimiter(options.HttpRoundTripMethodRateLimiter.Invoke()),

                // Operation call (fire and forget)
                MessageType.operation_call 
                    => _operationCallLimiter ??= new TokenBucketRateLimiter(options.HttpRoundTripMethodRateLimiter.Invoke()),

                // Subscription group (comparten el mismo limiter)
                MessageType.subscription_init 
                or MessageType.subscription_data 
                or MessageType.subscription_data_with_ack 
                or MessageType.subscription_complete 
                    => _subscriptionLimiter ??= new TokenBucketRateLimiter(options.HttpRoundTripMethodRateLimiter.Invoke()),
                
                // Stream group (todos comparten)
                MessageType.stream_init 
                or MessageType.stream_complete 
                or MessageType.stream_data 
                or MessageType.stream_data_ack 
                or MessageType.stream_data_with_ack 
                    => _streamLimiter ??= new TokenBucketRateLimiter(options.HttpRoundTripMethodRateLimiter.Invoke()),
                
                // Ingest group (comparten)
                MessageType.ingest_init 
                or MessageType.ingest_data 
                or MessageType.ingest_data_with_ack 
                or MessageType.ingest_complete 
                or MessageType.ingest_result 
                    => _ingestLimiter ??= new TokenBucketRateLimiter(options.HttpRoundTripMethodRateLimiter.Invoke()),
                
                _ => null,
            };
        }

        private HubconSettings? GetLinkedSettings(MessageType type, Guid id)
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
                    => settingsManager.GetSettings(id, () => WebsocketInvokeSettingsAttribute.Default()).Factory(),

                // Operation call (fire and forget)
                MessageType.operation_call
                    => settingsManager.GetSettings(id, () => WebsocketInvokeSettingsAttribute.Default()).Factory(),

                // Subscription group (comparten el mismo limiter)
                MessageType.subscription_init
                or MessageType.subscription_data
                or MessageType.subscription_data_with_ack
                or MessageType.subscription_complete
                    => settingsManager.GetSettings(id, () => SubscriptionSettingsAttribute.Default()).Factory(),

                // Stream group (todos comparten)
                MessageType.stream_init
                or MessageType.stream_complete
                or MessageType.stream_data
                or MessageType.stream_data_ack
                or MessageType.stream_data_with_ack
                    => settingsManager.GetSettings(id, () => StreamingSettingsAttribute.Default()).Factory(),

                // Ingest group (comparten)
                MessageType.ingest_init
                or MessageType.ingest_data
                or MessageType.ingest_data_with_ack
                or MessageType.ingest_complete
                or MessageType.ingest_result
                    => settingsManager.GetSettings(id, () => IngestSettingsAttribute.Default()).Factory(),

                _ => null,
            };
        }

        private HubconSettings? GetOperationSettings(MessageType type, IOperationEndpoint operation)
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
                    => settingsManager.GetSettings(operation, () => WebsocketInvokeSettingsAttribute.Default()).Factory(),

                // Operation call (fire and forget)
                MessageType.operation_call
                    => settingsManager.GetSettings(operation, () => WebsocketInvokeSettingsAttribute.Default()).Factory(),

                // Subscription group (comparten el mismo limiter)
                MessageType.subscription_init
                or MessageType.subscription_data
                or MessageType.subscription_data_with_ack
                or MessageType.subscription_complete
                    => settingsManager.GetSettings(operation, () => SubscriptionSettingsAttribute.Default()).Factory(),

                // Stream group (todos comparten)
                MessageType.stream_init
                or MessageType.stream_complete
                or MessageType.stream_data
                or MessageType.stream_data_ack
                or MessageType.stream_data_with_ack
                    => settingsManager.GetSettings(operation, () => StreamingSettingsAttribute.Default()).Factory(),

                // Ingest group (comparten)
                MessageType.ingest_init
                or MessageType.ingest_data
                or MessageType.ingest_data_with_ack
                or MessageType.ingest_complete
                or MessageType.ingest_result
                    => settingsManager.GetSettings(operation, () => IngestSettingsAttribute.Default()).Factory(),

                _ => null,
            };
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var limiter in _typeLimiters.Values)
                limiter.Dispose();

            _globalLimiter?.Dispose();
            _ingestLimiter?.Dispose();
            _streamLimiter?.Dispose();
            _subscriptionLimiter?.Dispose();
            _operationCallLimiter?.Dispose();
            _operationInvokeLimiter?.Dispose();

            linkedSettings.Clear();
            operationSettings.Clear();

            await Task.CompletedTask;
        }
    }
}
