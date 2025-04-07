using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Converters;
using Hubcon.Core.Handlers;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection.Metadata;
using System.Threading.Channels;

namespace Hubcon.SignalR.Client
{
    public abstract class BaseSignalRClientController : IHubconClientController, IHostedService
    {
        protected string _url = string.Empty;
        protected Func<HubConnection>? _hubFactory = null;
        protected CancellationToken _token;
        protected Task? runningTask = null;
        protected HubConnection? hub = null; 

        private bool IsBuilt { get; set; } = false;

        private IHubconControllerManager? _hubconController;
        public IHubconControllerManager HubconController { get => _hubconController!; set => _hubconController = value; }

        protected BaseSignalRClientController() { }

        protected BaseSignalRClientController(string url) => Build(url);
        

        private void Build(string url)
        {
            if (IsBuilt)
                return;

            var derivedType = GetType();
            if (!typeof(IBaseHubconController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IBaseHubconController)} o un tipo derivado.");

            _url = url;
            var hub = new HubConnectionBuilder()
                .WithUrl(url)
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();

            _hubFactory = () => hub;

            var commHandler = new SignalRClientCommunicationHandler(_hubFactory);
            HubconController = HubconControllerManager.GetControllerManager(commHandler);

            HubconController.Methods.BuildMethods(this, GetType(), (methodSignature, methodInfo, handler) =>
            {
                if (typeof(IAsyncEnumerable<object>).IsAssignableFrom(methodInfo.ReturnType))
                    hub?.On($"{methodSignature}", (Func<string, MethodInvokeRequest, Task>)StartStream);
                else if (methodInfo.ReturnType == typeof(void))
                    hub?.On($"{methodSignature}", (Func<MethodInvokeRequest, Task>)HubconController.Methods!.HandleWithoutResultAsync);
                else if (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    hub?.On($"{methodSignature}", (Func<MethodInvokeRequest, Task<MethodResponse>>)HubconController.Methods!.HandleWithResultAsync);
                else if (methodInfo.ReturnType == typeof(Task))
                    hub?.On($"{methodSignature}", (Func<MethodInvokeRequest, Task>)HubconController.Methods!.HandleWithoutResultAsync);
                else
                    hub?.On($"{methodSignature}", (Func<MethodInvokeRequest, Task<MethodResponse>>)HubconController.Methods!.HandleWithResultAsync);
            });

            IsBuilt = true;
        }

        public TICommunicationContract GetConnector<TICommunicationContract>() 
            where TICommunicationContract : ICommunicationContract

        {
            return new HubconServerConnector<TICommunicationContract, ICommunicationHandler>(HubconController.CommunicationHandler).GetClient()!;
        }

        public async Task<BaseSignalRClientController> StartInstanceAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            _ = StartAsync(url, consoleOutput, cancellationToken);

            while (true) 
            { 
                await Task.Delay(500, cancellationToken);

                if(hub?.State == HubConnectionState.Connected)
                {
                    return this;
                }
            }          
        }

        public async Task StartAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                Build(url ?? "localhost:5000/clienthub");

            hub = _hubFactory?.Invoke();
            try
            {
                _token = cancellationToken;

                bool connectedInvoked = false;
                while (true)
                {
                    await Task.Delay(3000, cancellationToken);
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
            var hub = _hubFactory?.Invoke();
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

        public async Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info) => await HubconController.Methods!.HandleWithResultAsync(info);
        public async Task HandleMethodVoid(MethodInvokeRequest info) => await HubconController.Methods!.HandleWithoutResultAsync(info);
        public async Task StartStream(string methodCode, MethodInvokeRequest info)
        {
            Console.WriteLine("StartStream llamado");
            var reader = HubconController.Methods!.GetStream(info);
            var channel = Channel.CreateUnbounded<object>();

            // Simulamos un productor que escribe en el canal
            _ = Task.Run(async () =>
            {
                await foreach(var item in reader)
                {
                    await channel.Writer.WriteAsync(DynamicConverter.SerializeData(item)!);
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
                if(_server == null)
                    return _server = new HubconServerConnector<TICommunicationContract, ICommunicationHandler>(HubconController.CommunicationHandler).GetClient();

                return _server;
            }
        }
    }
}
