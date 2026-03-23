using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the contract for a transport-level client responsible for dispatching requests 
    /// and managing communication streams within the Hubcon framework.
    /// </summary>
    public interface ITransportClient
    {
        /// <summary>
        /// Asynchronously sends a request that expects a single response of type <typeparamref name="T"/>.
        /// Typically used for standard RPC Round-Trip operations.
        /// </summary>
        /// <typeparam name="T">The expected type of the response data.</typeparam>
        /// <param name="request">The <see cref="IOperationRequest"/> containing the call metadata.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> for the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous send operation.</returns>
        public ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a fire-and-forget call where no response is expected from the server.
        /// </summary>
        /// <param name="request">The <see cref="IOperationRequest"/> containing the call metadata.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> for the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous execution.</returns>
        public ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Establishes a connection to a remote stream and returns an asynchronous enumerable of the results.
        /// </summary>
        /// <param name="request">The <see cref="IOperationRequest"/> initiating the stream.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> for the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> containing an <see cref="IAsyncEnumerable{T}"/> of <see cref="JsonElement"/>.</returns>
        public ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a high-throughput ingestion operation to push data of type <typeparamref name="T"/> to the server.
        /// </summary>
        /// <typeparam name="T">The type of the data being ingested.</typeparam>
        /// <param name="request">The <see cref="IOperationRequest"/> associated with the ingestion.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> for the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous ingestion.</returns>
        public ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes and builds the transport client using the provided <see cref="TransportContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="TransportContext"/> containing environment and configuration data.</param>
        public void Build(TransportContext context);

        /// <summary>
        /// Gets a value indicating whether the transport client has been successfully built and is ready for use.
        /// </summary>
        /// <returns><see langword="true"/> if the client is built; otherwise, <see langword="false"/>.</returns>
        public bool IsBuilt();
    }
}

namespace Hubcon
{
    /// <summary>
    /// Defines the contract for transports that support persistent, real-time connections, 
    /// providing lifecycle management for the connection state.
    /// </summary>
    public interface IRealTimeTransport
    {
        /// <summary>
        /// Asynchronously establishes a connection to the remote server.
        /// </summary>
        /// <param name="url">Optional. The target URL for the connection. If null, the default configured URL is used.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing the <see cref="HubconResponse"/> of the connection attempt.</returns>
        public Task<HubconResponse> Connect(string? url = null);

        /// <summary>
        /// Asynchronously attempts to reconnect to the specified URL after a disconnection.
        /// </summary>
        /// <param name="url">The target URL for the reconnection.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing the <see cref="HubconResponse"/> of the reconnection attempt.</returns>
        public Task<HubconResponse> Reconnect(string url);

        /// <summary>
        /// Asynchronously terminates the current connection.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> containing the <see cref="HubconResponse"/> of the disconnection operation.</returns>
        public Task<HubconResponse> Disconnect();

        /// <summary>
        /// Asynchronously checks whether the transport is currently connected to the server.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="HubconResponse{T}"/> with the connection status.</returns>
        public Task<HubconResponse<bool>> IsConnected();
    }

    /// <summary>
    /// Contains the configuration and environmental data required to initialize and build a transport client.
    /// </summary>
    public class TransportContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransportContext"/> class.
        /// </summary>
        public TransportContext(
            IServiceProvider proxyServiceProvider,
            IInterceptorManager interceptorManager,
            IClientOptions clientOptions,
            IContractOptions contractOptions,
            Uri uri,
            Func<IAuthenticationManager>? authenticationManagerFactory,
            string baseUrl,
            string originalUrl,
            bool useSecureConnection,
            IDynamicConverter converter,
            string webSocketUrl,
            string httpUrl)
        {
            ProxyServiceProvider = proxyServiceProvider;
            InterceptorManager = interceptorManager;
            ClientOptions = clientOptions;
            ContractOptions = contractOptions;
            Uri = uri;
            AuthenticationManagerFactory = authenticationManagerFactory;
            BaseUrl = baseUrl;
            OriginalUrl = originalUrl;
            UseSecureConnection = useSecureConnection;
            Converter = converter;
            WebSocketUrl = webSocketUrl;
            HttpUrl = httpUrl;
        }

        /// <summary>Gets the service provider for the proxy layer.</summary>
        public IServiceProvider ProxyServiceProvider { get; }
        /// <summary>Gets the interceptor manager for handling hooks and interceptors.</summary>
        public IInterceptorManager InterceptorManager { get; }
        /// <summary>Gets the global client options.</summary>
        public IClientOptions ClientOptions { get; }
        /// <summary>Gets the contract-specific options.</summary>
        public IContractOptions ContractOptions { get; }
        /// <summary>Gets the target <see cref="Uri"/> for the transport.</summary>
        public Uri Uri { get; }
        /// <summary>Gets the factory for creating authentication managers.</summary>
        public Func<IAuthenticationManager>? AuthenticationManagerFactory { get; }
        /// <summary>Gets the base URL of the service.</summary>
        public string BaseUrl { get; }
        /// <summary>Gets the original, unmodified URL.</summary>
        public string OriginalUrl { get; }
        /// <summary>Gets a value indicating whether SSL/TLS is enabled.</summary>
        public bool UseSecureConnection { get; }
        /// <summary>Gets the converter used for data serialization.</summary>
        public IDynamicConverter Converter { get; }
        /// <summary>Gets the resolved WebSocket URL.</summary>
        public string WebSocketUrl { get; }
        /// <summary>Gets the resolved HTTP URL.</summary>
        public string HttpUrl { get; }
    }

    /// <summary>
    /// Defines a transport client specialized for a specific <see cref="HubconTransportAttribute"/>.
    /// </summary>
    /// <typeparam name="TAttribute">The transport attribute type that this client handles.</typeparam>
    public interface ITransportClient<TAttribute> : ITransportClient
        where TAttribute : HubconTransportAttribute
    {
    }

    /// <summary>
    /// Provides a base implementation for transport clients, handling the management 
    /// of the <see cref="TransportContext"/> and providing abstract methods for operation execution.
    /// </summary>
    /// <typeparam name="TAttribute">The transport attribute type associated with this implementation.</typeparam>
    public abstract class TransportClient<TAttribute> : ITransportClient<TAttribute>
        where TAttribute : HubconTransportAttribute
    {
        private TransportContext? _context;

        /// <summary>
        /// Gets the <see cref="TransportContext"/> for the client. 
        /// Accessing this property before the client is built will result in a <see cref="NullReferenceException"/>.
        /// </summary>
        protected TransportContext Context { get => _context!; }

        /// <inheritdoc/>
        public abstract ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds the transport client using the provided context. 
        /// This method is called internally during the initialization of the client.
        /// </summary>
        /// <param name="context">The <see cref="TransportContext"/> to use.</param>
        void ITransportClient.Build(TransportContext context)
        {
            _context ??= context;
            Build(context);
        }

        /// <inheritdoc/>
        bool ITransportClient.IsBuilt()
        {
            return _context != null;
        }

        /// <summary>
        /// When overridden in a derived class, performs the specific logic required to initialize the transport.
        /// </summary>
        /// <param name="context">The configuration context.</param>
        protected abstract void Build(TransportContext context);
    }
}
