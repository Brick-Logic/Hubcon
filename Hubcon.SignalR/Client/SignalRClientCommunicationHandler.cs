using Hubcon.Core.Interfaces;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace Hubcon.SignalR.Client
{
    public class SignalRClientCommunicationHandler : ICommunicationHandler
    {
        protected Func<HubConnection> _hubFactory;

        public SignalRClientCommunicationHandler(Func<HubConnection> hubFactory)
        {
            _hubFactory = hubFactory;
        }

        public async Task<MethodResponse> InvokeAsync(string method, object[] arguments, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            if (client.State != HubConnectionState.Connected) await client.StartAsync();

            MethodInvokeRequest request = new MethodInvokeRequest(method, arguments).SerializeArgs();

            return await client.InvokeAsync<MethodResponse>(nameof(IHubconController.HandleTask), request, cancellationToken);
        }

        public async Task CallAsync(string method, object[] arguments, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            MethodInvokeRequest request = new MethodInvokeRequest(method, arguments).SerializeArgs();
            await client.SendAsync(nameof(IHubconController.HandleVoid), request, cancellationToken);
        }

        public async Task<IAsyncEnumerable<T>> StreamAsync<T>(string method, object[] arguments, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

            MethodInvokeRequest request = new MethodInvokeRequest(method, arguments).SerializeArgs();

            return client.StreamAsync<T>(nameof(IHubconTargetedClientController.HandleStream), request, cancellationToken);
        }

        public List<IClientReference> GetAllClients()
        {
            return Array.Empty<IClientReference>().ToList();
        }

        public ICommunicationHandler WithUserId(string id)
        {
            return this;
        }
    }
}
