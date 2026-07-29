using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Shared.Core.Websockets.Messages.Ping;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IMessageRouter
    {
        /// <summary>
        /// An event that's raised when the message router enters in an error state.
        /// </summary>
        public event EventHandler<Exception>? OnError;
        
        /// <summary>
        /// An event that's raised when the message router receives a pong message.
        /// </summary>
        public event EventHandler<PongMessage>? OnPongMessage;
        
        void BeginRequest(Guid id);
        IIngestSession<T> CreateIngest<T>(Guid id, string connectionId, IOperationRequest request, IOperationOptions operationOptions, Action? onFinishedCallback = null);
        IStreamSession<T> CreateStream<T>(Guid id, string connectionId, IOperationRequest request, Action? onFinishedCallback = null);
        ValueTask DisposeAsync();
        void EndRequest(Guid id);
        ValueTask<BaseMessage?> GetResponseAsync(Guid id, TimeSpan timeout, CancellationToken cancellationToken);
        void Start();
    }
}