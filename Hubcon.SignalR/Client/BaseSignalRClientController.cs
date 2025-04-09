using Hubcon.Core;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Converters;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Tools;
using Hubcon.SignalR.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Hubcon.SignalR.Client
{
    public abstract class BaseSignalRClientController : IHubconClientController
    {
        protected string _url = string.Empty;
        protected CancellationToken _token;
        protected Task? runningTask = null;
        protected HubConnection? hub = null;
        protected IServiceScope scope = null!;
        protected DynamicConverter _converter = null!;
        bool connectedInvoked = false;


        private bool IsBuilt { get; set; } = false;

        private IHubconControllerManager? _hubconController;
        public IHubconControllerManager HubconController { get => _hubconController!; set => _hubconController = value; }

        protected BaseSignalRClientController() { }

        protected BaseSignalRClientController(string url) => Build(url);


        private void Build(string url)
        {
            if (IsBuilt)
                return;

            StaticServiceProvider.Setup(new ServiceCollection(), services => 
            { 
                services.AddHubconClient();
                services.AddScoped<HubConnectionBuilder>();
                services.AddSingleton(x => 
                { 
                    return new HubConnectionBuilder()
                        .WithUrl(url)
                        .AddMessagePackProtocol()
                        .WithAutomaticReconnect()
                        .Build();
                });
                services.AddScoped(typeof(SignalRClientCommunicationHandler<>));
                services.AddScoped(typeof(HubconServerConnector<>));
                services.AddScoped(typeof(ServerConnectorInterceptor<>));
                services.AddSingleton(GetType(), x => this);
            });

            scope = StaticServiceProvider.Services.CreateScope();

            hub = scope.ServiceProvider.GetRequiredService<HubConnection>();

            Type communicationHandlerType = typeof(SignalRClientCommunicationHandler<>).MakeGenericType(hub.GetType());
            Type controllerManagerType = typeof(HubconControllerManager<>).MakeGenericType(communicationHandlerType);

            HubconController = (IHubconControllerManager)scope.ServiceProvider.GetRequiredService(controllerManagerType);
            
            _converter = scope.ServiceProvider.GetRequiredService<DynamicConverter>();

            var derivedType = GetType();
            if (!typeof(IBaseHubconController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IBaseHubconController)} o un tipo derivado.");

            _url = url;

            HubconController.Pipeline.RegisterMethods(GetType(), (methodSignature, methodInfo) =>
            {
                if (typeof(IAsyncEnumerable<object>).IsAssignableFrom(methodInfo.ReturnType))
                    hub?.On($"{methodSignature}", (Func<string, MethodInvokeRequest, Task>)StartStream);
                else if (methodInfo.ReturnType == typeof(void))
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => HubconController.Pipeline!.HandleWithoutResultAsync(this, request));
                else if (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => HubconController.Pipeline!.HandleWithResultAsync(this, request));
                else if (methodInfo.ReturnType == typeof(Task))
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => HubconController.Pipeline!.HandleWithoutResultAsync(this, request));
                else
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => HubconController.Pipeline!.HandleWithResultAsync(this, request));
            });

            IsBuilt = true;
        }

        public TICommunicationContract GetConnector<TICommunicationContract>()
            where TICommunicationContract : ICommunicationContract

        {
            Type communicationHandlerType = typeof(HubconServerConnector<>).MakeGenericType(GetType());
            return ((IServerConnector)scope.ServiceProvider.GetRequiredService(communicationHandlerType)).GetClient<TICommunicationContract>();
        }

        public async Task<BaseSignalRClientController> StartInstanceAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            //await StartAsync(url, consoleOutput, cancellationToken);
            var startTask = StartAsync(url, consoleOutput, cancellationToken); // NO awaited aún

            while (true)
            {
                if (startTask.IsFaulted)
                {
                    // Si la task falló, lanzar la excepción
                    throw startTask.Exception!.GetBaseException();
                }

                if (hub?.State == HubConnectionState.Connected)
                {
                    while (!connectedInvoked);
                    return this;
                }

                await Task.Delay(100, cancellationToken); // Evitar busy-wait
            }
        }

        public async Task StartAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                Build(url ?? "localhost:5000/clienthub");

            try
            {
                _token = cancellationToken;
                while (true)
                {
                    await Task.Delay(2000, cancellationToken);
                    if (hub?.State == HubConnectionState.Connecting)
                    {
                        consoleOutput?.Invoke($"Connecting to {_url}...");
                        _ = hub.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (hub?.State == HubConnectionState.Disconnected)
                    {
                        consoleOutput?.Invoke($"Failed connecting to {_url}. Retrying...");
                        _ = hub.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (hub?.State == HubConnectionState.Reconnecting)
                    {
                        consoleOutput?.Invoke($"Connection lost, reconnecting to {_url}...");
                        _ = hub.StartAsync(_token);
                        connectedInvoked = false;
                    }
                    else if (hub?.State == HubConnectionState.Connected && !connectedInvoked)
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

            _ = hub?.StopAsync(_token);
        }
        public void Stop()
        {
            _ = hub?.StopAsync(_token);
        }

        public Task StartAsync(string? url = null, CancellationToken cancellationToken = default)
        {
            runningTask = Task.Run(async () => await StartAsync(url, Console.WriteLine, cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return runningTask ?? Task.CompletedTask;
        }

        public async Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info) => await HubconController.Pipeline!.HandleWithResultAsync(this, info);
        public async Task HandleMethodVoid(MethodInvokeRequest info) => await HubconController.Pipeline!.HandleWithoutResultAsync(this, info);
        public async Task StartStream(string methodCode, MethodInvokeRequest info)
        {
            Console.WriteLine("StartStream llamado");
            var reader = HubconController.Pipeline!.GetStream(this, info);
            var channel = Channel.CreateUnbounded<object>();

            // Simulamos un productor que escribe en el canal
            _ = Task.Run(async () =>
            {
                await foreach (var item in reader)
                {
                    await channel.Writer.WriteAsync(_converter.SerializeData(item)!);
                }
                channel.Writer.Complete(); // Indica que no habrá más datos
            });

            await hub!.SendAsync(nameof(IHubconServerController.ReceiveStream), methodCode, channel.Reader);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartAsync(null, cancellationToken);
        }
    }

    public class BaseSignalRClientController<TICommunicationContract> : BaseSignalRClientController
        where TICommunicationContract : ICommunicationContract
    {
        private TICommunicationContract? _server;
        public TICommunicationContract Server
        {
            get
            {
                if (_server == null)
                {
                    Type communicationHandlerType = typeof(HubconServerConnector<>).MakeGenericType(hub!.GetType());
                    return _server = ((HubconServerConnector<IBaseHubconController>)scope.ServiceProvider.GetRequiredService(communicationHandlerType)).GetClient<TICommunicationContract>();
                }

                return _server;
            }
        }
    }
}
