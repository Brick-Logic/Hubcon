using Hubcon.Models;
using Microsoft.AspNetCore.SignalR;

namespace Hubcon.Default
{
    public class HubconDefaultHub : HubconDefaultHub<object>
    {
    }

    public class HubconDefaultHub<T> : Hub where T : class, new()
    {
        public delegate void OnClientsChangedEventHandler();
        public delegate void OnClientConnectedEventHandler(string ConnectionId);
        public delegate void OnClientDisconnectedEventHandler(string ConnectionId);

        public static event OnClientsChangedEventHandler? ClientsChanged;
        public static event OnClientConnectedEventHandler? OnClientConnected;
        public static event OnClientDisconnectedEventHandler? OnClientDisconnected;

        public static Dictionary<string, ClientReference<T>> ClientReferences { get; } = [];
        public static IEnumerable<ClientReference<T>> GetClients() => ClientReferences.Values;


        public override Task OnConnectedAsync()
        {
            ClientReferences.Add(Context.ConnectionId, new ClientReference<T>(Context.ConnectionId, null));
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
        public static void SetClientInfo(string clientId, T clientInfo)
        {
            if(ClientReferences.ContainsKey(clientId))
                ClientReferences[clientId].ClientInfo = clientInfo;
        }
    }
}
