using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientOptions
    {
        public Uri? BaseUri { get; }
        public List<Type> Contracts { get; }
        public Type? AuthenticationManagerType { get; }
        public string? HttpPrefix { get; }
        public string? WebsocketPrefix { get; }
        public Action<ClientWebSocketOptions>? WebSocketOptions { get; }
        public Action<HttpClient>? HttpClientOptions { get; }
        public bool UseSecureConnection { get; }
        TimeSpan? WebsocketPingInterval { get; }
    }
}
