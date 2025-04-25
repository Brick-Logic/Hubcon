using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.SignalR.Server
{
    public class SignalRServerCommunicationHandler<THub>: IServerCommunicationHandler where THub : Hub
    {
        private string TargetClientId { get; set; } = string.Empty;

        [HubconInject]
        private IHubContext<THub> hubContext { get; set; }

        private Type hubType { get => hubContext.GetType().GetGenericArguments()[0]; }

        [HubconInject]
        private StreamNotificationHandler streamNotificationHandler { get; }

        public async Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken) 
        { 
            IMethodResponse result;
            var client = hubContext.Clients.Client(TargetClientId);

            result = await client.InvokeAsync<BaseMethodResponse>(request.MethodName!, request, cancellationToken);
            return result;                   
        }

        public async Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            var client = hubContext.Clients.Client(TargetClientId);

            await client.SendAsync(request.MethodName!, request, cancellationToken);
        }

        public List<IClientReference> GetAllClients()
        {
            return BaseHubController.GetClients(hubType).ToList();
        }

        public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            var client = hubContext.Clients.Client(TargetClientId);

            var code = Guid.NewGuid().ToString();

            _ = client.SendAsync(request.MethodName!, code, request, cancellationToken);

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
