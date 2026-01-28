using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Server.Core.Entrypoint
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DefaultEntrypoint
    {
        public static Task<IHubconResponse> HandleMethodWithResult(IOperationRequest request, TransportAttribute transport, IServiceProvider serviceProvider, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithResultAsync(request, transport, wrapper, cancellationToken);
        }

        public static Task<IHubconResponse> HandleMethodVoid(IOperationRequest request, TransportAttribute transport, IServiceProvider serviceProvider, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithoutResultAsync(request, transport, wrapper, cancellationToken);
        }

        public static Task<IHubconResponse> HandleMethodStream(IOperationRequest request, TransportAttribute transport, IServiceProvider serviceProvider, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetStream(request, transport, wrapper, cancellationToken);
        }

        public static Task<IHubconResponse> HandleSubscription(IOperationRequest request, TransportAttribute transport, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetSubscription(request, transport, cancellationToken);
        }

        public static Task<IHubconResponse> HandleIngest(IOperationRequest request, TransportAttribute transport, IServiceProvider serviceProvider, Dictionary<Guid, object> sources, object? wrapper = null, CancellationToken cancellationToken = default)
        {
            var requestHandler = serviceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleIngest(request, transport, sources, wrapper, cancellationToken);
        }
    }
}
