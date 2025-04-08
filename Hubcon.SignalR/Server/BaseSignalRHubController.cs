using Castle.DynamicProxy.Contributors;
using Hubcon.Core.Controllers;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Registries;
using Hubcon.Core.Tools;
using Hubcon.SignalR.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Hubcon.SignalR.Server
{
    public abstract class BaseHubController : Hub, IHubconServerController
    {
        // Events
        public static event OnClientConnectedEventHandler? OnClientConnected;
        public static event OnClientDisconnectedEventHandler? OnClientDisconnected;

        public delegate void OnClientConnectedEventHandler(Type hubType, string connectionId);
        public delegate void OnClientDisconnectedEventHandler(Type hubType, string connectionId);

        // Clients
        protected static Dictionary<Type, Dictionary<string, ClientReference>> ClientReferences { get; } = new();

        public IHubconControllerManager HubconController { get; }

        protected BaseHubController()
        {
            var commHandler = new SignalRServerCommunicationHandler(GetType());
            HubconController = HubconControllerManager.GetControllerManager(commHandler);
            HubconController.Pipeline.RegisterMethods(GetType());

            ClientReferences.TryAdd(GetType(), new());
        }

        public async Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info) 
            => await HubconController.Pipeline.HandleWithResultAsync(this, info);
        public async Task HandleMethodVoid(MethodInvokeRequest info) 
            => await HubconController.Pipeline.HandleWithoutResultAsync(this, info);
        public async Task ReceiveStream(string code, ChannelReader<object> reader) 
            => await StreamNotificationHandler.NotifyStream(code, reader);
        public IAsyncEnumerable<object> HandleMethodStream(MethodInvokeRequest info) 
            => HubconController.Pipeline.GetStream(this, info);

        protected IEnumerable<IClientReference> GetClients() => ClientReferences[GetType()].Values;
        public static IEnumerable<IClientReference> GetClients(Type hubType) => ClientReferences[hubType].Values;

        public override Task OnConnectedAsync()
        {
            ClientReferences[GetType()].Add(Context.ConnectionId, new ClientReference(Context.ConnectionId));
            OnClientConnected?.Invoke(GetType(), Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ClientReferences[GetType()].Remove(Context.ConnectionId);
            OnClientDisconnected?.Invoke(GetType(), Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }

    public abstract class BaseHubController<TICommunicationContract> : BaseHubController
        where TICommunicationContract : ICommunicationContract
    {

        private IClientAccessor _clientAccessor = default!;
        protected IClientAccessor ClientAccessor 
        { 
            get
            {
                if (_clientAccessor == null)
                {
                    Type clientManagerType = typeof(IClientAccessor<,>).MakeGenericType(typeof(TICommunicationContract), GetType());
                    using (var scope = StaticServiceProvider.Services.CreateScope())
                    {
                        var scopedProvider = scope.ServiceProvider;
                        _clientAccessor = (IClientAccessor)scopedProvider.GetRequiredService(clientManagerType);
                    }
                }

                return _clientAccessor;
            } 
        }
        protected TICommunicationContract Client => clientRegistry.TryGetClient<TICommunicationContract>(Context.ConnectionId)!;    
        protected TICommunicationContract GetClient(string connectionId) => clientRegistry.TryGetClient<TICommunicationContract>(connectionId)!;

        private readonly ClientRegistry clientRegistry = new();

        public override Task OnConnectedAsync()
        {
            var client = ClientAccessor.GetClient<TICommunicationContract>(Context.ConnectionId);
            clientRegistry.RegisterClient(Context.ConnectionId, client);

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            clientRegistry.UnregisterClient(Context.ConnectionId);

            return base.OnDisconnectedAsync(exception);
        }
    }
}
