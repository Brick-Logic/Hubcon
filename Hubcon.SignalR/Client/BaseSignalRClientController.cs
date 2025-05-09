using Autofac;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Connectors;
using Hubcon.Core.Extensions;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.SignalR.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.SignalR.Client
{
    public abstract class BaseSignalRClientController
    {
        protected string _url = string.Empty;
        protected CancellationToken _token;
        protected Task? runningTask = null;
        protected HubConnection? hub = null;
        protected IServiceProvider serviceProvider = null!;
        protected IDynamicConverter _converter = null!;
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

            //serviceProvider = Hubcon.Core.Builders.DependencyInjection.CreateHubconServiceProvider(this, container =>
            //{
            //    container.RegisterWithInjector(x => x.RegisterInstance
            //    (
            //        new HubConnectionBuilder()
            //            .WithUrl(url)
            //            .WithAutomaticReconnect()
            //            .Build()

            //        ).As(typeof(HubConnection)).AsSingleton())
            //        .RegisterWithInjector(x => x.RegisterGeneric(typeof(SignalRClientCommunicationHandler<>)).AsSingleton())
            //        .RegisterWithInjector(x => x.RegisterInstance(this).As(GetType()).AsSingleton());
            //}, options);

            lifetimeScope = serviceProvider.GetRequiredService<ILifetimeScope>();
            hub = serviceProvider.GetRequiredService<HubConnection>();
            HubconController = serviceProvider.GetRequiredService<IHubconControllerManager>()!;        
            _converter = serviceProvider.GetRequiredService<IDynamicConverter>()!;
            var _invokerProvider = serviceProvider.GetRequiredService<IMethodDescriptorProvider>()!;


            var derivedType = GetType();
            if (!typeof(IBaseHubconController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IBaseHubconController)} o un tipo derivado.");

            _url = url;


            _invokerProvider.OnMethodRegistered += ((IMethodDescriptor descriptor) =>
            {
                if (typeof(IAsyncEnumerable<>).IsAssignableFrom(descriptor.InternalMethodInfo.ReturnType))
                    hub?.On($"{nameof(StartStream)}", (Func<string, MethodInvokeRequest, Task>)StartStream);

                else if (descriptor.InternalMethodInfo.ReturnType == typeof(void))
                    hub?.On($"{nameof(IControllerInvocationHandler.HandleWithoutResultAsync)}", (MethodInvokeRequest request)
                        => GetControllerManager().Pipeline!.HandleWithoutResultAsync(request));

                else if (descriptor.InternalMethodInfo.ReturnType.IsGenericType && descriptor.InternalMethodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    hub?.On($"{nameof(IControllerInvocationHandler.HandleWithResultAsync)}", (MethodInvokeRequest request)
                        => GetControllerManager().Pipeline!.HandleWithResultAsync(request));

                else if (descriptor.InternalMethodInfo.ReturnType == typeof(Task))
                    hub?.On($"{nameof(IControllerInvocationHandler.HandleWithoutResultAsync)}", (MethodInvokeRequest request)
                        => GetControllerManager().Pipeline!.HandleWithoutResultAsync(request));

                else
                    hub?.On($"{nameof(IControllerInvocationHandler.HandleWithResultAsync)}", (MethodInvokeRequest request)
                        => GetControllerManager().Pipeline!.HandleWithResultAsync(request));
            });

            IsBuilt = true;
        }

        public TICommunicationContract GetConnector<TICommunicationContract>()
            where TICommunicationContract : IControllerContract

        {
            Type connectorType = typeof(IHubconServerConnector<>).MakeGenericType(GetType(), typeof(SignalRClientCommunicationHandler<HubConnection>));
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartAsync(null, null, null, cancellationToken);
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

        public async Task<IMethodResponse<JsonElement>> HandleMethodTask(IMethodInvokeRequest info) => await GetControllerManager().Pipeline!.HandleWithResultAsync(info);
        public async Task<IResponse> HandleMethodVoid(IMethodInvokeRequest info) => await GetControllerManager().Pipeline!.HandleWithoutResultAsync(info);
        public async Task<IResponse> StartStream(string methodCode, IMethodInvokeRequest info)
        {
            var reader = HubconController.Pipeline!.GetStream(info);
            var channel = Channel.CreateUnbounded<object>();

            // Simulamos un productor que escribe en el canal
            _ = Task.Run(async () =>
            {
                await foreach (var item in reader)
                {
                    await channel.Writer.WriteAsync(_converter.SerializeObject(item)!);
                }
                channel.Writer.Complete(); // Indica que no habrá más datos
            });

            await hub!.SendAsync("ReceiveStream", methodCode, channel.Reader);

            return new BaseMethodResponse(true);
        }
        
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(IMethodInvokeRequest info)
        {
            throw new NotImplementedException();
        }

        public void Build(WebApplication? app = null)
        {
            
        }

        public IAsyncEnumerable<JsonElement?> HandleSubscription(ISubscriptionRequest request)
        {
            throw new NotImplementedException();
        }
    }

    public class BaseSignalRClientController<TICommunicationContract> : BaseSignalRClientController/*,  IHubconClientController<SignalRClientCommunicationHandler<HubConnection>>*/
        where TICommunicationContract : IControllerContract
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
                    Type communicationHandlerType = typeof(HubconServerConnector<>).MakeGenericType(hubType, communicationHandler);
                    return _server = ((IServerConnector)serviceProvider.GetRequiredService(communicationHandlerType)!).GetClient<TICommunicationContract>();
                }

                return _server;
            }
        }
    }
}
