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

namespace Hubcon.Client.Core.Websockets
{
    /// <summary>
    /// Represents a stream session and manages all the resources needed.
    /// </summary>
    public abstract class StreamSession : IStreamSession, IDisposable
    {
        public abstract BaseMessage Payload { get; }

        /// <summary>
        /// Used to provide the next element to the ongoing stream session.
        /// </summary>
        /// <param name="streamDataData"></param>
        public abstract void Next(JsonElement streamDataData);
        
        /// <summary>
        /// Tries to complete the current stream session.
        /// </summary>
        public abstract void TryComplete();
        
        /// <summary>
        /// Allows to configure a callback when the provided cancellation token is canceled.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        public abstract void AddCancellation(Action callback, CancellationToken cancellationToken);
        
        /// <inheritdoc/>
        public abstract void Dispose();
    } 
    
    
    // var tcs = new CancellationTokenSource();
    //
    // HeartbeatWatcher hw = null!;
    //         
    // var registration = cancellationToken.Register(async () =>
    // {
    //     if (remoteCancelEnabled && _webSocket.State == WebSocketState.Open)
    //         await _sender.SendMessageAsync(new CancelMessage(request.Id, connectionId), cancellationToken);
    //
    //     tcs.Cancel();
    // });
    //
    // hw = new HeartbeatWatcher(TimeSpan.Zero, async () =>
    // {
    //             
    //             
    //     if (_streams.TryRemove(request.Id, out var obs))
    //     {
    //         obs.Item1.OnCompleted();
    //         if (!obs.Item2.IsCancellationRequested)
    //         {
    //             obs.Item2.Cancel();
    //             obs.Item2.Dispose();
    //         }
    //         obs.Item4.Dispose();
    //     }
    // });
    //
    // var observable = new GenericObservable<T>(
    //     null!,
    //     request.Id,
    //     converter.SerializeToElement(request),
    //     RequestType.Stream,
    //     converter,
    //     async () => await hw.DisposeAsync(),
    //     options.ReconnectStreams);


    /// <inheritdoc cref="StreamSession" />
    public sealed class StreamSession<T> : StreamSession, IStreamSession<T>, IDisposable
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