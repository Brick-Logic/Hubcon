using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Server.Core.Entrypoint
{
    /// <summary>
    /// Provides a centralized, static entry point for dispatching Hubcon operations.
    /// This class is designed for use by transport layers to route incoming requests to the appropriate <see cref="IRequestHandler"/> methods.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DefaultEntrypoint
    {
        /// <summary>
        /// Dispatches a standard RPC method call that expects a return value.
        /// </summary>
        public static ValueTask<IHubconResponse> HandleMethodWithResult(IOperationRequest request, HubconTransportAttribute transport, IServiceProvider serviceProvider, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithResultAsync(request, transport, wrapper, cancellationToken);
        }

        /// <summary>
        /// Dispatches an RPC method call that does not return a value (void or Task).
        /// </summary>
        public static ValueTask<IHubconResponse> HandleMethodVoid(IOperationRequest request, HubconTransportAttribute transport, IServiceProvider serviceProvider, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithoutResultAsync(request, transport, wrapper, cancellationToken);
        }

        /// <summary>
        /// Dispatches a request to open an asynchronous data stream.
        /// </summary>
        public static ValueTask<IHubconResponse> HandleMethodStream(IOperationRequest request, HubconTransportAttribute transport, IServiceProvider serviceProvider, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetStream(request, transport, wrapper, cancellationToken);
        }

        /// <summary>
        /// Dispatches a high-throughput ingestion request, mapping unique source identifiers 
        /// to their corresponding data objects.
        /// </summary>
        public static ValueTask<IHubconResponse> HandleIngest(IOperationRequest request, HubconTransportAttribute transport, IServiceProvider serviceProvider, Dictionary<Guid, object> sources, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleIngest(request, transport, sources, wrapper, cancellationToken);
        }
    }
}
