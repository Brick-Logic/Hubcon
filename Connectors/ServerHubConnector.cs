using Castle.DynamicProxy;
using Hubcon.Connectors;
using Hubcon.Interceptors;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon
{
    /// <summary>
    /// The ServerHubConnector allows a client to connect itself to a ServerHub and control its methods given its URL and
    /// the server's interface.
    /// </summary>
    /// <typeparam name="TIServerHubController"></typeparam>
    public class ServerHubConnector<TIServerHubController> : HubconClientBuilder<TIServerHubController>, IConnector
        where TIServerHubController : IServerHubController
    {
        private TIServerHubController? _client;
        private readonly HubConnection _hubConnection;

        public TIServerHubController Instance
        {
            get
            {
                _client ??= GetInstance();
                return _client;
            }
        }

        public HubConnection Connection { get => _hubConnection; }

        public ServerHubConnector(string url) : base()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(url)
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();
        }
        public ServerHubConnector(HubConnection connection) : base()
        {
            _hubConnection = connection;
        }

        public async Task StartAsync()
        {
            await _hubConnection.StartAsync();
        }

        public async Task StopAsync()
        {
            await _hubConnection.StopAsync();
        }

        private TIServerHubController GetInstance()
        {
            var proxyGenerator = new ProxyGenerator();
            return (TIServerHubController)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TIServerHubController),
                (TIServerHubController)DynamicImplementationCreator.CreateImplementation(typeof(TIServerHubController)),
                new ServerHubConnectorInterceptor(Connection)
            );
        }
    }
}
