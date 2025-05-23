using Hubcon.SignalR.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.SignalR.Client
{
    //public class SignalRClientCommunicationHandler<THubConnection> : ICommunicationHandler
    //    where THubConnection : HubConnection
    //{
    //    protected Func<HubConnection> _hubFactory;
    //    private readonly IDynamicConverter _converter;

    //    public SignalRClientCommunicationHandler(THubConnection hubFactory, IDynamicConverter converter)
    //    {
    //        _hubFactory = () => hubFactory;
    //        _converter = converter;
    //    }

    //    public async Task<IOperationResponse<JsonElement>> InvokeAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
    //    {
    //        var client = _hubFactory.Invoke();

    //        if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

    //        return await client.InvokeAsync<BaseJsonResponse>(request.OperationName!, request, cancellationToken);
    //    }

    //    public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
    //    {
    //        var client = _hubFactory.Invoke();
    //        await client.SendAsync(request.OperationName!, request, cancellationToken);
    //    }
        
    //    public async Task<IAsyncEnumerable<T?>> StreamAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
    //    {
    //        var client = _hubFactory.Invoke();

    //        if (client.State != HubConnectionState.Connected) await client.StartAsync(cancellationToken);

    //        return await client.StreamAsync<T>(request, _converter, cancellationToken);
    //    }
    //}
}
