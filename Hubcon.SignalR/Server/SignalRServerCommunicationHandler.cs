using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Hubcon.Core.Tools;
using Hubcon.Core.Interfaces;
using Hubcon.Core.Models;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Handlers;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.SignalR.Server
{
    public class SignalRServerCommunicationHandler : IServerCommunicationHandler
    {
        private protected string TargetClientId { get; private set; } = string.Empty;
        private protected Func<IHubContext<BaseHubController>>? HubContextFactory { get; private set; }
        private protected Func<BaseHubController>? HubFactory { get; private set; }
        private protected Type HubType { get; private set; }

        public SignalRServerCommunicationHandler(Type hubType)
        {
            HubType = hubType;
            Type hubContextType = typeof(IHubContext<>).MakeGenericType(hubType);
            var hubContext = (IHubContext<BaseHubController>)StaticServiceProvider.Services.GetRequiredService(hubContextType);
            HubContextFactory = () => hubContext;
        }

        public SignalRServerCommunicationHandler(BaseHubController hubFactory)
        {
            HubType = hubFactory.GetType();
            HubFactory = () => hubFactory;
        }

        public async Task<MethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken) 
        { 
            MethodResponse result;
            Hub? hub = HubFactory?.Invoke();
            IHubContext<Hub>? hubContext = HubContextFactory?.Invoke();

            var client = hubContext?.Clients.Client(TargetClientId) ?? hub!.Clients.Client(TargetClientId);

            result = await client.InvokeAsync<MethodResponse>(request.HandlerMethodName!, request, cancellationToken);
            return result;                   
        }

        public async Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            BaseHubController? hub = HubFactory?.Invoke();
            IHubContext<BaseHubController>? hubContext = HubContextFactory?.Invoke();

            var client = hubContext?.Clients.Client(TargetClientId) ?? hub!.Clients.Client(TargetClientId);

            await client.SendAsync(request.HandlerMethodName!, request, cancellationToken);
        }

        public List<IClientReference> GetAllClients()
        {
            return BaseHubController.GetClients(HubType).ToList();
        }

        public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            BaseHubController? hub = HubFactory?.Invoke();
            IHubContext<BaseHubController>? hubContext = HubContextFactory?.Invoke();

            var client = hubContext?.Clients.Client(TargetClientId) ?? hub!.Clients.Client(TargetClientId);

            var code = Guid.NewGuid().ToString();

            _ = client.SendAsync(request.HandlerMethodName!, code, request, cancellationToken);

            var stream = await StreamHandler.WaitStreamAsync<T>(code);
            return stream;
        }

        public IServerCommunicationHandler WithClientId(string clientId)
        {
            TargetClientId = clientId;
            return this;
        }
    }
}
