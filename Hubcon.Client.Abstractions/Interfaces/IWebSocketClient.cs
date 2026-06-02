using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconWebSocket : IAsyncDisposable
    {
        ClientWebSocket WebSocket { get; }
        IMessageSender Sender { get; }
        IMessageReceiver Receiver { get; }
        WebSocketState State { get; }
        string ConnectionId { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default);
        ValueTask DisposeAsync();
        Task<IIngestSession<T>> GetIngestSession<T>(IOperationRequest operationRequest, bool useRemoteCancel, IOperationOptions? operationOptions = null, CancellationToken cancellationToken = default);
        Task<IStreamSession<T>> GetStreamSession<T>(IOperationRequest payload, bool useRemoteCancel, CancellationToken cancellationToken = default);
        Task<BaseMessage?> SendAndReceiveAsync<TRequest>(TRequest message, bool useRemoteCancel, CancellationToken cancellationToken = default) where TRequest : BaseMessage;
        Task SendAsync<TRequest>(TRequest message, bool useRemoteCancel, CancellationToken cancellationToken = default) where TRequest : BaseMessage;
    }
}