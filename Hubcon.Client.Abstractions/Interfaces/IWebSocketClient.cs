using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Represents the interface for a connected WebSocket client for the Hubcon platform.
    /// This interface defines the core lifecycle, message handling, and session management
    /// capabilities necessary for real-time communication.
    /// </summary>
    public interface IHubconWebSocket : IAsyncDisposable
    {
        /// <summary>
        /// Gets the underlying <see cref="ClientWebSocket"/> instance used for the connection.
        /// </summary>
        ClientWebSocket WebSocket { get; }

        /// <summary>
        /// Gets the component responsible for sending messages to the hub.
        /// </summary>
        IMessageSender Sender { get; }

        /// <summary>
        /// Gets the component responsible for receiving and processing incoming messages from the hub.
        /// </summary>
        IMessageReceiver Receiver { get; }

        /// <summary>
        /// Gets the current state of the WebSocket connection (e.g., Open, Connecting, Closed).
        /// </summary>
        WebSocketState State { get; }

        /// <summary>
        /// Gets the unique identifier assigned to this client's connection session.
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// Asynchronously attempts to establish a connection to the specified URI.
        /// </summary>
        /// <param name="uri">The target URI for the WebSocket connection.</param>
        /// <param name="authToken">The authentication token for the WebSocket connection. Use null if the server does not require authentication.</param>
        /// <param name="cancellationToken">A token that may be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous connection operation.</returns>
        ValueTask ConnectAsync(Uri uri, string? authToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the current connection, if the connection is established.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task DisconnectAsync();

        /// <summary>
        /// Retrieves an ingest session for streaming operational requests of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of the operation request payload.</typeparam>
        /// <param name="operationRequest">The request details for the operation.</param>
        /// <param name="useRemoteCancel">Whether to use a remote cancellation token for this session.</param>
        /// <param name="operationOptions">Optional settings for the operation.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>A <see cref="Task{TResult}"/> that resolves to an <see cref="IIngestSession{T}"/>.</returns>
        IIngestSession<T> GetIngestSession<T>(IOperationRequest operationRequest, bool useRemoteCancel,
            IOperationOptions? operationOptions = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a streaming session for sending and receiving operational payloads.
        /// </summary>
        /// <typeparam name="T">The type of the payload.</typeparam>
        /// <param name="payload">The initial payload for the stream.</param>
        /// <param name="useRemoteCancel">Whether to use a remote cancellation token for this session.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>A <see cref="Task{TResult}"/> that resolves to an <see cref="IStreamSession{T}"/>.</returns>
        ValueTask<IStreamSession<T>> GetStreamSession<T>(IOperationRequest payload, bool useRemoteCancel,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request message of type TRequest and awaits the corresponding response message.
        /// </summary>
        /// <typeparam name="TRequest">The expected base message type for the request.</typeparam>
        /// <param name="message">The message payload to send.</param>
        /// <param name="useRemoteCancel">Whether to use a remote cancellation token.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>A <see cref="Task"/> that resolves to the received response message.</returns>
        ValueTask<BaseMessage?> SendAndReceiveAsync<TRequest>(TRequest message, bool useRemoteCancel,
            CancellationToken cancellationToken = default) where TRequest : BaseMessage;

        /// <summary>
        /// Sends a request message of type TRequest and awaits the corresponding response message.
        /// </summary>
        /// <typeparam name="TRequest">The expected base message type for the request.</typeparam>
        /// <param name="message">The message payload to send.</param>
        /// <param name="useRemoteCancel">Whether to use a remote cancellation token.</param>
        /// <param name="timeout">The request timeout.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>A <see cref="Task"/> that resolves to the received response message.</returns>
        ValueTask<BaseMessage?> SendAndReceiveAsync<TRequest>(TRequest message, bool useRemoteCancel, TimeSpan timeout,
            CancellationToken cancellationToken = default) where TRequest : BaseMessage;

        /// <summary>
        /// Sends a message of type TRequest to the hub without expecting an immediate response.
        /// </summary>
        /// <typeparam name="TRequest">The expected base message type for the request.</typeparam>
        /// <param name="message">The message payload to send.</param>
        /// <param name="useRemoteCancel">Whether to use a remote cancellation token.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous send operation.</returns>
        ValueTask SendAsync<TRequest>(TRequest message, bool useRemoteCancel,
            CancellationToken cancellationToken = default) where TRequest : BaseMessage;
    }
}