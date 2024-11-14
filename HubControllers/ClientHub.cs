using Hubcon.Controllers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hubcon
{
    public abstract class ClientController : HubController, IHostedService
    {
        protected HubConnection _hubConnection;
        protected CancellationToken _token;
        protected string _url;

        protected ClientController(string url)
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
            return new ServerHubConnector<TIServerHubController>(_hubConnection).Instance;
        }

        public ClientController StartInstanceAsync(Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            _ = StartAsync(consoleOutput, cancellationToken);
            return this;
        }

        public async Task StartAsync(Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _token = cancellationToken;

                bool connectedInvoked = false;
                while (true)
                {
                    await Task.Delay(1000, cancellationToken);
                    if (_hubConnection.State == HubConnectionState.Connecting)
                    {
                        consoleOutput?.Invoke($"Connecting to {_url}...");
                        _ = _hubConnection.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (_hubConnection.State == HubConnectionState.Disconnected)
                    {
                        consoleOutput?.Invoke($"Failed connectin to {_url}. Retrying...");
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartAsync(Console.WriteLine, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopAsync(cancellationToken);
        }
    }

    public class ClientController<TIServerHubController> : ClientController
        where TIServerHubController : IServerHubController
    {
        public TIServerHubController Server { get; private set; }
        public ClientController(string url) : base(url) => Server = GetConnector<TIServerHubController>();
    }
}
