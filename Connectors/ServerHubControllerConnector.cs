using Castle.DynamicProxy;
using Hubcon.Interceptors;
using Hubcon.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Connectors
{
    public class ServerHubControllerConnector<TIServerHubController> : HubconControllerConnector<TIServerHubController>, IConnector
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

        public ServerHubControllerConnector(string url) : base()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(url)
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();
        }
        public ServerHubControllerConnector(HubConnection connection) : base()
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
                new ServerHubControllerConnectorInterceptor(Connection)
            );
        }
    }
}
