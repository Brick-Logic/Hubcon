using Hubcon.Core.Converters;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.SignalR.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;

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

        public async Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

            return await client.InvokeAsync<BaseMethodResponse>(request.MethodName!, request, cancellationToken);
        }

        public async Task CallAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();
            await client.SendAsync(request.MethodName!, request, cancellationToken);
        }
        
        public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var client = _hubFactory.Invoke();

            if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

            return await client.StreamAsync<T>(request, _converter, cancellationToken);
        }
    }
}
