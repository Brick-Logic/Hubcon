using Hubcon.Core.Converters;
using Hubcon.Core.Handlers;
using Hubcon.Core.Interfaces;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;
using Hubcon.SignalR.Extensions;

namespace Hubcon.SignalR.Client
{
    public class SignalRClientCommunicationHandler : ICommunicationHandler
    {
        protected Func<HubConnection> _hubFactory;

        public SignalRClientCommunicationHandler(Func<HubConnection> hubFactory)
        {
            _hubFactory = hubFactory;
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

            return await client.StreamAsync<T>(request, cancellationToken);
        }
    }
}
