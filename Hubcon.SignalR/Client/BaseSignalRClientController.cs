using Autofac;
using Autofac.Core.Lifetime;
using Hubcon.Core;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Converters;
using Hubcon.Core.Injectors;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Middleware;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Models.Middleware;
using Hubcon.Core.Models.Pipeline.Interfaces;
using Hubcon.SignalR.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Hubcon.SignalR.Client
{
    public abstract class BaseSignalRClientController : IHubconClientController<SignalRClientCommunicationHandler<HubConnection>>
    {
        protected string _url = string.Empty;
        protected CancellationToken _token;
        protected Task? runningTask = null;
        protected HubConnection? hub = null;
        protected IServiceProvider serviceProvider = null!;
        protected DynamicConverter _converter = null!;
        bool connectedInvoked = false;


        private readonly TaskCompletionSource _connectedTcs = new();
        public Task WaitUntilConnectedAsync() => _connectedTcs.Task;


        private bool IsBuilt { get; set; } = false;

        public Task InternalTask { get; private set; } = null!;

        public IHubconControllerManager HubconController { get; private set; } = null!;

        private ILifetimeScope lifetimeScope = null!;

        protected BaseSignalRClientController() { }

        protected BaseSignalRClientController(
            string url, 
            Action<ContainerBuilder>? additionalServices = null, 
            Action<IMiddlewareOptions>? options = null) => Build(url, additionalServices, options);

        private IHubconControllerManager GetControllerManager() => lifetimeScope.BeginLifetimeScope().Resolve<IHubconControllerManager>();

        private void Build(string url, Action<ContainerBuilder>? additionalServices = null, Action<IMiddlewareOptions>? options = null)
        {
            if (IsBuilt)
                return;

            serviceProvider = Hubcon.Core.DependencyInjection.CreateHubconServiceProvider(this, container =>
            {
                container.RegisterWithInjector(x => x.RegisterInstance
                (
                    new HubConnectionBuilder()
                        .WithUrl(url)
                        .AddMessagePackProtocol()
                        .WithAutomaticReconnect()
                        .Build()

                    ).As(typeof(HubConnection)).AsSingleton())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(SignalRClientCommunicationHandler<>)).AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType(typeof(HubconControllerManager<SignalRClientCommunicationHandler<HubConnection>>)).As(typeof(IHubconControllerManager)).AsSingleton())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(ServerConnectorInterceptor<,>)).AsSingleton())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconServerConnector<,>)).AsSingleton())
                    .RegisterWithInjector(x => x.RegisterInstance(this).As(GetType()).AsSingleton());
            },
            options => 
            {
                options.AddMiddleware<LoggingMiddleware>();
            });

            lifetimeScope = serviceProvider.GetRequiredService<ILifetimeScope>();
            hub = serviceProvider.GetRequiredService<HubConnection>();
            HubconController = serviceProvider.GetRequiredService<IHubconControllerManager>()!;        
            _converter = serviceProvider.GetRequiredService<DynamicConverter>()!;


            var derivedType = GetType();
            if (!typeof(IBaseHubconController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IBaseHubconController)} o un tipo derivado.");

            _url = url;

            HubconController.Pipeline.RegisterMethods(GetType(), (methodSignature, methodInfo) =>
            {
                if (typeof(IAsyncEnumerable<object>).IsAssignableFrom(methodInfo.ReturnType))
                    hub?.On($"{methodSignature}", (Func<string, MethodInvokeRequest, Task>)StartStream);
                else if (methodInfo.ReturnType == typeof(void))
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => GetControllerManager().Pipeline!.HandleWithoutResultAsync(this, request));
                else if (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => GetControllerManager().Pipeline!.HandleWithResultAsync(this, request));
                else if (methodInfo.ReturnType == typeof(Task))
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => GetControllerManager().Pipeline!.HandleWithoutResultAsync(this, request));
                else
                    hub?.On($"{methodSignature}", (MethodInvokeRequest request) => GetControllerManager().Pipeline!.HandleWithResultAsync(this, request));
            });

            IsBuilt = true;
        }

        public TICommunicationContract GetConnector<TICommunicationContract>()
            where TICommunicationContract : ICommunicationContract

        {
            Type connectorType = typeof(HubconServerConnector<,>).MakeGenericType(GetType(), typeof(SignalRClientCommunicationHandler<HubConnection>));
            IServerConnector connector = (IServerConnector)serviceProvider.GetRequiredService(connectorType)!;
            return connector.GetClient<TICommunicationContract>();
        }

        public async Task<BaseSignalRClientController> StartInstanceAsync(
            string? url = null, 
            Action<string>? consoleOutput = null, 
            Action<ContainerBuilder>? additionalServices = null, 
            Action<IMiddlewareOptions>? options = null, 
            CancellationToken cancellationToken = default)
        {
            //await StartAsync(url, consoleOutput, cancellationToken);
            InternalTask = StartAsync(url, consoleOutput, additionalServices, options, cancellationToken);

            while (true)
            {
                if (InternalTask.IsFaulted)
                {
                    // Si la task falló, lanzar la excepción
                    throw InternalTask.Exception!;
                }

                if (hub?.State == HubConnectionState.Connected)
                {
                    while (!connectedInvoked)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                    return this;
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        public async Task StartAsync(string? url = null, 
            Action<string>? consoleOutput = null, 
            Action<ContainerBuilder>? additionalServices = null, 
            Action<IMiddlewareOptions>? options = null, 
            CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                Build(url ?? "localhost:5000/clienthub", additionalServices, options);

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
                        await Task.Delay(10000, cancellationToken);
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

        public Task StartAsync(string? url = null, Action<ContainerBuilder>? additionalServices = null, Action<IMiddlewareOptions>? options = null, CancellationToken cancellationToken = default)
        {
            runningTask = Task.Run(async () => await StartAsync(url, Console.WriteLine, additionalServices, options, cancellationToken), cancellationToken);
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
            await StartAsync(null, null, null, cancellationToken);
        }
    }

    public class BaseSignalRClientController<TICommunicationContract> : BaseSignalRClientController,  IHubconClientController<SignalRClientCommunicationHandler<HubConnection>>
        where TICommunicationContract : ICommunicationContract
    {
        private TICommunicationContract? _server;
        public TICommunicationContract Server
        {
            get
            {
                if (_server == null)
                {
                    Type communicationHandler = typeof(SignalRServerCommunicationHandler<>).MakeGenericType(GetType());
                    Type hubType = hub!.GetType().MakeGenericType(communicationHandler);
                    Type communicationHandlerType = typeof(HubconServerConnector<,>).MakeGenericType(hubType, communicationHandler);
                    return _server = ((IServerConnector)serviceProvider.GetRequiredService(communicationHandlerType)!).GetClient<TICommunicationContract>();
                }

                return _server;
            }
        }
    }
}
