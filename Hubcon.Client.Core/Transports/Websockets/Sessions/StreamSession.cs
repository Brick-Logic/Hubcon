using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Generic;

namespace Hubcon.Client.Core.Transports.Websockets.Sessions
{
    /// <summary>
    /// Represents a stream session and manages all the resources needed.
    /// </summary>
    internal abstract class StreamSession : IStreamSession, IDisposable
    {
        public abstract BaseMessage Payload { get; }
        
        /// <inheritdoc/>
        public abstract void Next(JsonElement streamDataData);
        
        /// <inheritdoc/>
        public abstract void TryComplete();
        
        /// <inheritdoc/>
        public abstract void AddCancellation(Action callback, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public abstract void AddCancellation(Action<object?> callback, object? state, CancellationToken cancellationToken);
        
        /// <inheritdoc/>
        public abstract void Dispose();
    } 

    /// <inheritdoc cref="StreamSession" />
    internal sealed class StreamSession<T> : StreamSession, IStreamSession<T>, IDisposable
    {
        private readonly GenericObservable<T> _observable;
        private readonly CancellationTokenSource _cts;
        private readonly BaseMessage _payload;
        private readonly Action? _onFinishedCallback;
        private readonly HeartbeatWatcher _heartbeatWatcher;
        private CancellationTokenRegistration? _cancellationTrigger;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="context"></param>
        /// <param name="onFinishedCallback"></param>
        public StreamSession(BaseMessage payload, TransportContext context, Action? onFinishedCallback = null)
        {
            _cts = new CancellationTokenSource();
            _payload = payload;
            _onFinishedCallback = onFinishedCallback;
            _observable = new GenericObservable<T>(
                null, 
                payload.Id, 
                context.Converter.SerializeToElement(payload), 
                RequestType.Stream,
                context.Converter,
                () =>
                {
                    TryComplete();
                    Dispose();
                });
            
            _heartbeatWatcher = new HeartbeatWatcher(context.ClientOptions.WebsocketTimeout,  () =>
            {
                TryComplete();
                Dispose();
                return Task.CompletedTask;
            });
        }

        public override BaseMessage Payload => _payload;

        /// <inheritdoc/>
        public override void Next(JsonElement streamData)
        {
            _observable.OnNextElement(streamData);
            _heartbeatWatcher.NotifyHeartbeat();
        }
        
        /// <inheritdoc/>
        public override void TryComplete()
        {
            _observable.OnCompleted();
            _cts.Cancel();
        }
        
        /// <inheritdoc/>
        public override void AddCancellation(Action callback, CancellationToken cancellationToken)
        {
            _cancellationTrigger ??= cancellationToken.Register(callback);
        }
        
        /// <inheritdoc/>
        public override void AddCancellation(Action<object?> callback, object? state, CancellationToken cancellationToken)
        {
            _cancellationTrigger ??= cancellationToken.Register(callback, state);
        }
        
        /// <summary>
        /// Gets an <see cref="IObservable{T}"/> object used to consume the stream elements.
        /// </summary>
        /// <returns></returns>
        public IObservable<T> GetObservable()
        {
            return _observable;
        }
        
        /// <inheritdoc/>
        public override void Dispose()
        {
            _observable.OnCompleted();
            _cts.Cancel();
            _ = _heartbeatWatcher.DisposeAsync();
            _cancellationTrigger?.Dispose();
            _cts.Dispose();
            _payload.Dispose();
            _onFinishedCallback?.Invoke();

            GC.SuppressFinalize(this);
        }
    }
}