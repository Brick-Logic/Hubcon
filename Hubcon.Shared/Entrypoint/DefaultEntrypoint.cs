using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using System.Text.Json;

namespace Hubcon.Shared.Entrypoint
{
    public class DefaultEntrypoint
    {

        [HubconInject]
        public IRequestHandler RequestHandler { get; }

        public Task<IOperationResponse<JsonElement>> HandleMethodWithResult(IOperationRequest request)
            => RequestHandler.HandleWithResultAsync(request);

        public Task<IResponse> HandleMethodVoid(IOperationRequest request)
            => RequestHandler.HandleWithoutResultAsync(request);

        public Task<IAsyncEnumerable<object?>> HandleMethodStream(IOperationRequest request) 
            => RequestHandler.GetStream(request);
        
        public Task<IAsyncEnumerable<object?>> HandleSubscription(IOperationRequest request)
            => RequestHandler.GetSubscription(request);

        public Task HandleIngest(IOperationRequest request, Dictionary<string, object> sources)
            => RequestHandler.HandleIngest(request, sources);

        public void Build()
        {
        }
    }
}
