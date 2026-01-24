using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Server.Core.Entrypoint
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DefaultEntrypoint(IServiceProvider ServiceProvider)
    {
        public Task<IHubconResponse> HandleMethodWithResult(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithResultAsync(request, null, cancellationToken);
        }

        public Task<IHubconResponse> HandleMethodVoid(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithoutResultAsync(request, null, cancellationToken);
        }

        public Task<IHubconResponse> HandleMethodStream(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetStream(request, null, cancellationToken);
        }

        public Task<IHubconResponse> HandleSubscription(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetSubscription(request, cancellationToken);
        }

        public Task<IHubconResponse> HandleIngest(IOperationRequest request, Dictionary<Guid, object> sources, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleIngest(request, sources, null, cancellationToken);
        }

        public void Build()
        {
        }
    }
}
