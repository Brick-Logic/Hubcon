using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Operation;

namespace Hubcon.Client.Core.Websockets.Operations
{
    /// <summary>
    /// Represents an operation of type Call, which sends a message without excepting a response.
    /// </summary>
    public static class CallOperation
    {
        public static async Task<HubconResponse> Execute(IOperationRequest request, WebSocketManager webSocket, CancellationToken cancellationToken)
        {
            await webSocket.EnsureConnectedAsync();

            webSocket.Sender.SendMessageAsync(request, remoteCancelEnabled, cancellationToken);
        }
    }
}