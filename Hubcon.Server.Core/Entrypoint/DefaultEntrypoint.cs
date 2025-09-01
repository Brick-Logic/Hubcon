using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Server.Core.Entrypoint
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DefaultEntrypoint(IServiceProvider ServiceProvider)
    {
        public Task<IOperationResponse<JsonElement>> HandleMethodWithResult(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithResultAsync(request, cancellationToken);
        }

        public Task<IResponse> HandleMethodVoid(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleWithoutResultAsync(request, cancellationToken);
        }

        public Task<IOperationResponse<IAsyncEnumerable<object?>?>> HandleMethodStream(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetStream(request, cancellationToken);
        }
        
        public Task<IOperationResponse<IAsyncEnumerable<object?>?>> HandleSubscription(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.GetSubscription(request, cancellationToken);
        }

        public Task<IOperationResponse<JsonElement>> HandleIngest(IOperationRequest request, Dictionary<Guid, object> sources, CancellationToken cancellationToken = default)
        {
            using var scope = ServiceProvider.CreateScope();
            var requestHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
            return requestHandler.HandleIngest(request, sources, cancellationToken);
        }
     
        public void Build()
        {
        }
    }
}
