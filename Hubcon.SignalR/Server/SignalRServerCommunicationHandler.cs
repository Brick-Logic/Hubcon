using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Attributes;
using Hubcon.Core.Invocation;
using Microsoft.AspNetCore.SignalR;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.SignalR.Server
{
    public class SignalRServerCommunicationHandler<THub>: IServerCommunicationHandler where THub : Hub
    {
        private string TargetClientId { get; set; } = string.Empty;

        [HubconInject]
        private IHubContext<THub> hubContext { get; set; }

        private Type hubType { get => hubContext.GetType().GetGenericArguments()[0]; }

        [HubconInject]
        private IStreamNotificationHandler streamNotificationHandler { get; }

        public async Task<IOperationResponse<JsonElement>> InvokeAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken) 
        { 
            IOperationResponse<JsonElement> result;
            var client = hubContext.Clients.Client(TargetClientId);

            result = await client.InvokeAsync<BaseJsonResponse>(request.OperationName!, request, cancellationToken);
            return result;                   
        }

        public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var client = hubContext.Clients.Client(TargetClientId);

            await client.SendAsync(request.OperationName!, request, cancellationToken);
        }

        public List<IClientReference> GetAllClients()
        {
            return BaseHubController.GetClients(hubType).ToList();
        }

        public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var client = hubContext.Clients.Client(TargetClientId);

            var code = Guid.NewGuid().ToString();

            _ = client.SendAsync(request.OperationName!, code, request, cancellationToken);

            var stream = await streamNotificationHandler.WaitStreamAsync<T>(code);
            return stream;
        }

        public IServerCommunicationHandler WithClientId(string clientId)
        {
            TargetClientId = clientId;
            return this;
        }
    }
}
