using Hubcon.Core.Converters;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.SignalR.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Hubcon.SignalR.Client
{
    public class SignalRClientCommunicationHandler<THubConnection> : ICommunicationHandler
        where THubConnection : HubConnection
    {
        protected Func<HubConnection> _hubFactory;
        private readonly DynamicConverter _converter;

        public SignalRClientCommunicationHandler(THubConnection hubFactory, DynamicConverter converter)
        {
            _hubFactory = () => hubFactory;
            _converter = converter;
        }

        public async Task<MethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

            return await client.InvokeAsync<MethodResponse>(request.HandlerMethodName!, request, cancellationToken);
        }

        public async Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();
            await client.SendAsync(request.HandlerMethodName!, request, cancellationToken);
        }
        
        public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

            return await client.StreamAsync<T>(request, _converter, cancellationToken);
        }
    }
}
