using Hubcon.Controllers;
using Hubcon.Models.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon
{
    public abstract class ClientHubController : HubController
    {
        protected HubConnection _hubConnection;
        protected CancellationToken _token;
        protected string _url;

        protected ClientHubController(string url)
        {
            _url = url;
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_url)
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();

            Build(_hubConnection);
        }

        public TIServerHubController GetConnector<TIServerHubController>() where TIServerHubController : IServerHubController
        {
            return new ServerHubControllerConnector<TIServerHubController>(_hubConnection).Instance;
        }

        public async Task StartAsync(Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _token = cancellationToken;

                _ = _hubConnection.StartAsync(_token);

                bool connectedInvoked = false;
                while (true)
                {
                    await Task.Delay(1000, cancellationToken);
                    if (_hubConnection.State == HubConnectionState.Connecting)
                    {
                        consoleOutput?.Invoke($"Connecting to {_url}...");
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Disconnected)
                    {
                        consoleOutput?.Invoke($"Disconnected. Trying connecting to {_url}...");
                        _ = _hubConnection.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Reconnecting)
                    {
                        consoleOutput?.Invoke($"Connection lost, reconnecting to {_url}...");
                        _ = _hubConnection.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Connected && !connectedInvoked)
                    {
                        consoleOutput?.Invoke($"Successfully connected to {_url}.");
                        connectedInvoked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                consoleOutput?.Invoke($"Error: {ex.Message}");

                if (_token.IsCancellationRequested)
                {
                    consoleOutput?.Invoke("Cancelado.");
                }
            }

            _ = _hubConnection?.StopAsync(_token);
        }
        public void Stop()
        {
            _ = _hubConnection?.StopAsync(_token);
        } 
    }

    public class ClientHubController<TIServerHubController> : ClientHubController
        where TIServerHubController : IServerHubController
    {
        public TIServerHubController Server { get; private set; }
        public ClientHubController(string url) : base(url) => Server = GetConnector<TIServerHubController>();
    }
}
