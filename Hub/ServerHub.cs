using Hubcon.Connectors;
using Hubcon.Controllers;
using Hubcon.Models;
using Microsoft.AspNetCore.SignalR;

namespace Hubcon
{
    public class ServerHub<TIClientHubController> : ServerHub where TIClientHubController : IHubController
    {
#pragma warning disable S2743 // Static fields should not be used in generic types
        public static new Dictionary<string, ClientReference> ClientReferences { get; } = [];
#pragma warning restore S2743 // Static fields should not be used in generic types

        public TIClientHubController Client { get => GetConnector(); }
        public TIClientHubController GetConnector()
        {
            return new ClientHubControllerConnector<TIClientHubController, ServerHub>(this).GetInstance(Context.ConnectionId);
        }
    }

    public abstract class ServerHub : Hub, IServerHubController
    {
        public delegate void OnClientsChangedEventHandler();
        public delegate void OnClientConnectedEventHandler(string ConnectionId);
        public delegate void OnClientDisconnectedEventHandler(string ConnectionId);

        public static event OnClientsChangedEventHandler? ClientsChanged;
        public static event OnClientConnectedEventHandler? OnClientConnected;
        public static event OnClientDisconnectedEventHandler? OnClientDisconnected;

        protected MethodHandler handler = new();

        public static Dictionary<string, ClientReference> ClientReferences { get; } = [];
        public static IEnumerable<ClientReference> GetClients() => ClientReferences.Values;

        protected ServerHub() => Build();
        private void Build() => handler.BuildMethods(this, GetType());

        public override Task OnConnectedAsync()
        {
            ClientReferences.Add(Context.ConnectionId, new ClientReference(Context.ConnectionId));
            ClientsChanged?.Invoke();
            OnClientConnected?.Invoke(Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ClientReferences.Remove(Context.ConnectionId);
            ClientsChanged?.Invoke();
            OnClientDisconnected?.Invoke(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
        public static void SetClientInfo(string clientId, object clientInfo)
        {
            if(ClientReferences.TryGetValue(clientId, out var value))
                value.ClientInfo = clientInfo;
        }

        public async Task<MethodResponse> HandleTask(MethodInvokeInfo info)
        {
            return await handler.HandleWithResultAsync(info);
        }

        public async Task HandleVoid(MethodInvokeInfo info)
        {
            await handler.HandleWithoutResultAsync(info);
        }
    }
}
