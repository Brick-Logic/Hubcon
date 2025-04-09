using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.SignalR.Server
{
    public class SignalRServerCommunicationHandler<THub> : IServerCommunicationHandler
        where THub : BaseHubController
    {
        private readonly StreamNotificationHandler _streamNotificationHandler;
        private protected string TargetClientId { get; private set; } = string.Empty;
        private protected Func<IHubContext<THub>> HubContextFactory { get; private set; }
        private protected Type HubType { get; private set; }

        public SignalRServerCommunicationHandler(IHubContext<THub> hubContext, StreamNotificationHandler streamNotificationHandler)
        {
            HubType = hubContext.GetType().GetGenericArguments()[0];
            HubContextFactory = () => hubContext;
            _streamNotificationHandler = streamNotificationHandler;
        }

        public async Task<MethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken) 
        { 
            MethodResponse result;
            IHubContext<THub> hubContext = HubContextFactory.Invoke();
            var client = hubContext.Clients.Client(TargetClientId);

            result = await client.InvokeAsync<MethodResponse>(request.HandlerMethodName!, request, cancellationToken);
            return result;                   
        }

        public async Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            IHubContext<THub> hubContext = HubContextFactory.Invoke();

            var client = hubContext.Clients.Client(TargetClientId);

            await client.SendAsync(request.HandlerMethodName!, request, cancellationToken);
        }

        public List<IClientReference> GetAllClients()
        {
            return BaseHubController.GetClients(HubType).ToList();
        }

        public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            IHubContext<THub> hubContext = HubContextFactory.Invoke();

            var client = hubContext.Clients.Client(TargetClientId);

            var code = Guid.NewGuid().ToString();

            _ = client.SendAsync(request.HandlerMethodName!, code, request, cancellationToken);

            var stream = await _streamNotificationHandler.WaitStreamAsync<T>(code);
            return stream;
        }

        public IServerCommunicationHandler WithClientId(string clientId)
        {
            TargetClientId = clientId;
            return this;
        }
    }
}
