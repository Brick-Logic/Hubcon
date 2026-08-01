using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;
#pragma warning disable CS1591

namespace Hubcon.Server.Core.RateLimiting
{
    // public class ScopedRateLimiterManager(
    //     ISettingsManager settingsManager, 
    //     IInternalServerOptions options, 
    //     IOperationConfigRegistry operationConfigRegistry,
    //     IOperationRegistry operationRegistry
    //     ) : IScopedRateLimiterManager, IAsyncDisposable
    // {
    //     private readonly ConcurrentDictionary<MessageType, RateLimiter> _typeLimiters = new();
    //
    //     private readonly ConcurrentDictionary<IOperationEndpoint, RateLimitAttribute> operationLimiters = new();
    //     private readonly ConcurrentDictionary<Guid, IOperationEndpoint> linkedSettings = new();
    //
    //     private readonly RateLimiter? _globalLimiter = new TokenBucketRateLimiter(options.GlobalRateLimiterOptions);
    //     private RateLimiter? _ingestLimiter = null;
    //     private RateLimiter? _streamLimiter = null;
    //     private RateLimiter? _subscriptionLimiter = null;
    //     private RateLimiter? _operationCallLimiter = null;
    //     private RateLimiter? _operationInvokeLimiter = null;
    //     private RateLimiter? _tokenUpdateLimiter = null;
    //
    //     public async ValueTask<bool> TryAcquireAsync(MessageType type, HubconTransportAttribute transport, IOperationRequest? operation = null)
    //     {
    //         try
    //         {
    //             if (options.ThrottlingIsDisabled)
    //                 return true;
    //
    //             if (_globalLimiter is not null)
    //                 await _globalLimiter.AcquireAsync();
    //
    //             if (_typeLimiters.TryGetValue(type, out var typeLimiter))
    //             {
    //                 await typeLimiter.AcquireAsync();
    //             }
    //             else
    //             {
    //                 var limiter = GetLimiterForMessageType(type, transport);
    //                 if (limiter is not null)
    //                 {
    //                     _typeLimiters[type] = limiter;
    //                     await limiter.AcquireAsync();
    //                 }
    //             }
    //
    //             if (operation is not null)
    //             {
    //                 var settings = operationLimiters.GetOrAdd(operation, x => GetOperationSettings(type, transport, operation)!);
    //                 await settings.RateBucket.AcquireAsync();
    //             }
    //
    //             return true;
    //         }
    //         catch (Exception)
    //         {
    //             return false;
    //         }
    //     }
    //
    //     public async ValueTask<bool> TryAcquireAsync(MessageType type, Guid messageId)
    //     {
    //         try
    //         {
    //             if (options.ThrottlingIsDisabled)
    //                 return true;
    //
    //             if (_globalLimiter is not null)
    //                 await _globalLimiter.AcquireAsync();
    //
    //             if (_typeLimiters.TryGetValue(type, out var typeLimiter))
    //             {
    //                 await typeLimiter.AcquireAsync();
    //             }
    //             else
    //             {
    //                 var limiter = GetLimiterForMessageType(type, null);
    //                 if (limiter is not null)
    //                 {
    //                     _typeLimiters[type] = limiter;
    //                     await limiter.AcquireAsync();
    //                 }
    //             }
    //
    //             if (messageId != Guid.Empty)
    //             {
    //                 linkedSettings.TryGetValue(messageId, out IOperationEndpoint? operationEndpoint);
    //
    //                 if (operationEndpoint != null)
    //                 {
    //                     var settings = operationLimiters.GetOrAdd(operationEndpoint, x => GetLinkedSettings(type, messageId)!);
    //                     await settings.RateBucket.AcquireAsync();
    //                 }
    //             }
    //
    //             return true;
    //         }
    //         catch (Exception)
    //         {
    //             return false;
    //         }
    //     }
    //
    //     public ValueTask Link(Guid id, HubconTransportAttribute transportAttribute, IOperationRequest request)
    //     {
    //         operationRegistry.TryGetOperationBlueprint(request, transportAttribute, out var value);
    //         operationConfigRegistry.Link(id, value!);
    //         linkedSettings.TryAdd(id, request);
    //         return ValueTask.CompletedTask;
    //     }
    //
    //     public ValueTask Unlink(Guid id)
    //     {
    //         operationConfigRegistry.Unlink(id);
    //         linkedSettings.TryRemove(id, out _);
    //         return ValueTask.CompletedTask;
    //     }
    //
    //     private RateLimiter? GetLimiterForMessageType(MessageType type, HubconTransportAttribute transport)
    //     {
    //         if (!options.TransportSettings.TryGetValue(transport, out var settings))
    //             settings = transport.DefaultTransportSettings;
    //         
    //         return type switch
    //         {
    //             MessageType.connection_ack
    //             or MessageType.connection_init
    //             or MessageType.pong
    //             or MessageType.error
    //             or MessageType.ack
    //             or MessageType.ingest_init_ack
    //             or MessageType.ingest_data_ack
    //             or MessageType.operation_response
    //                 => null,
    //
    //             // // Ping limiter (para evitar abuso)
    //             // MessageType.ping => settings.PingOperationLimiterOptions,
    //             //
    //             // // Operation messages (round-trip)
    //             // MessageType.operation_invoke
    //             //     => _operationInvokeLimiter ??= new TokenBucketRateLimiter(options.WebsocketRoundTripMethodRateLimiter.Invoke()),
    //             //
    //             // // Operation call (fire and forget)
    //             // MessageType.operation_call
    //             //     => _operationCallLimiter ??= new TokenBucketRateLimiter(options.WebsocketRoundTripMethodRateLimiter.Invoke()),
    //             //
    //             // // Subscription group (comparten el mismo limiter)
    //             // MessageType.subscription_init
    //             // or MessageType.subscription_data
    //             // or MessageType.subscription_data_with_ack
    //             // or MessageType.subscription_complete
    //             //     => _subscriptionLimiter ??= new TokenBucketRateLimiter(options.WebsocketSubscriptionRateLimiter.Invoke()),
    //             //
    //             // // Stream group (todos comparten)
    //             // MessageType.stream_init
    //             // or MessageType.stream_complete
    //             // or MessageType.stream_data
    //             // or MessageType.stream_data_ack
    //             // or MessageType.stream_data_with_ack
    //             //     => _streamLimiter ??= new TokenBucketRateLimiter(options.WebsocketStreamingRateLimiter.Invoke()),
    //             //
    //             // // Ingest group (comparten)
    //             // MessageType.ingest_init
    //             // or MessageType.ingest_data
    //             // or MessageType.ingest_data_with_ack
    //             // or MessageType.ingest_complete
    //             // or MessageType.ingest_result
    //             //     => _ingestLimiter ??= new TokenBucketRateLimiter(options.WebsocketRoundTripMethodRateLimiter.Invoke()),
    //             //
    //             // MessageType.token_update => _tokenUpdateLimiter ??= new TokenBucketRateLimiter(options.WebsocketTokenUpdateRateLimiter.Invoke()),
    //
    //             _ => _globalLimiter,
    //         };
    //     }
    //
    //     private RateLimitAttribute? GetLinkedSettings(MessageType type, Guid id)
    //     {
    //         // No limiters (inicialización, ack, errores, pong, etc.)
    //         return type switch
    //         {
    //             MessageType.connection_ack
    //             or MessageType.connection_init
    //             or MessageType.pong
    //             or MessageType.error
    //             or MessageType.ack
    //             or MessageType.ingest_init_ack
    //             or MessageType.ingest_data_ack
    //             or MessageType.operation_response
    //                 => null,
    //
    //             // Ping limiter (para evitar abuso)
    //             MessageType.ping
    //                 => null,
    //
    //             // Operation messages (round-trip)
    //             MessageType.operation_invoke
    //                 => settingsManager.GetSettings(id, () => new RateLimitAttribute()),
    //
    //             // Operation call (fire and forget)
    //             MessageType.operation_call
    //                 => settingsManager.GetSettings(id, () => new RateLimitAttribute()),
    //
    //             // Subscription group (comparten el mismo limiter)
    //             MessageType.subscription_init
    //             or MessageType.subscription_data
    //             or MessageType.subscription_data_with_ack
    //             or MessageType.subscription_complete
    //                 => settingsManager.GetSettings(id, () => new RateLimitAttribute()),
    //
    //             // Stream group (todos comparten)
    //             MessageType.stream_init
    //             or MessageType.stream_complete
    //             or MessageType.stream_data
    //             or MessageType.stream_data_ack
    //             or MessageType.stream_data_with_ack
    //                 => settingsManager.GetSettings(id, () => new RateLimitAttribute()),
    //
    //             // Ingest group (comparten)
    //             MessageType.ingest_init
    //             or MessageType.ingest_data
    //             or MessageType.ingest_data_with_ack
    //             or MessageType.ingest_complete
    //             or MessageType.ingest_result
    //                 => settingsManager.GetSettings(id, () => new RateLimitAttribute()),
    //
    //             _ => null,
    //         };
    //     }
    //
    //     private RateLimitAttribute? GetOperationSettings(MessageType type, HubconTransportAttribute transportAttribute, IOperationEndpoint operation)
    //     {
    //         // No limiters (inicialización, ack, errores, pong, etc.)
    //         return type switch
    //         {
    //             MessageType.connection_ack
    //             or MessageType.connection_init
    //             or MessageType.pong
    //             or MessageType.error
    //             or MessageType.ack
    //             or MessageType.ingest_init_ack
    //             or MessageType.ingest_data_ack
    //             or MessageType.operation_response
    //                 => null,
    //
    //             // Ping limiter (para evitar abuso)
    //             MessageType.ping
    //                 => null,
    //
    //             // Operation messages (round-trip)settingsManager.GetSettings(id, () => new RateLimitAttribute()),
    //             MessageType.operation_invoke
    //                 => settingsManager.GetSettings(operation, transportAttribute, () => new RateLimitAttribute()),
    //
    //             // Operation call (fire and forget)
    //             MessageType.operation_call
    //                 => settingsManager.GetSettings(operation, transportAttribute, () => new RateLimitAttribute()),
    //
    //             // Subscription group (comparten el mismo limiter)
    //             MessageType.subscription_init
    //             or MessageType.subscription_data
    //             or MessageType.subscription_data_with_ack
    //             or MessageType.subscription_complete
    //                 => settingsManager.GetSettings(operation, transportAttribute, () => new RateLimitAttribute()),
    //
    //             // Stream group (todos comparten)
    //             MessageType.stream_init
    //             or MessageType.stream_complete
    //             or MessageType.stream_data
    //             or MessageType.stream_data_ack
    //             or MessageType.stream_data_with_ack
    //                 => settingsManager.GetSettings(operation, transportAttribute, () => new RateLimitAttribute()),
    //
    //             // Ingest group (comparten)
    //             MessageType.ingest_init
    //             or MessageType.ingest_data
    //             or MessageType.ingest_data_with_ack
    //             or MessageType.ingest_complete
    //             or MessageType.ingest_result
    //                 => settingsManager.GetSettings(operation, transportAttribute, () => new RateLimitAttribute()),
    //
    //             _ => null,
    //         };
    //     }
    //
    //     public async ValueTask DisposeAsync()
    //     {
    //         foreach (var limiter in _typeLimiters.Values)
    //             limiter.Dispose();
    //
    //         _globalLimiter?.Dispose();
    //         _ingestLimiter?.Dispose();
    //         _streamLimiter?.Dispose();
    //         _subscriptionLimiter?.Dispose();
    //         _operationCallLimiter?.Dispose();
    //         _operationInvokeLimiter?.Dispose();
    //
    //         linkedSettings.Clear();
    //         operationLimiters.Clear();
    //
    //         await Task.CompletedTask;
    //     }
    // }
}