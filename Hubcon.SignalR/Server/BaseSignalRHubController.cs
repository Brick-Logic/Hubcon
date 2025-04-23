using Autofac;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Registries;
using Hubcon.SignalR.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
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

        private IHubconControllerManager _hubconController = null!;
        public IHubconControllerManager HubconController 
        {   get
            {
                if(_hubconController == null)
                {
                    var hubType = typeof(SignalRServerCommunicationHandler<>).MakeGenericType(GetType());
                    var type = typeof(IHubconControllerManager<>).MakeGenericType(hubType);
                    _hubconController = (IHubconControllerManager)ServiceProvider.Resolve(type);
                }

                return _hubconController;
            }
        }

        [HubconInject]
        public StreamNotificationHandler StreamNotificationHandler { get; }

        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }


        public async Task<IMethodResponse> HandleMethodTask(MethodInvokeRequest info) 
            => await HubconController.Pipeline.HandleWithResultAsync(this, info);
        public async Task HandleMethodVoid(MethodInvokeRequest info) 
            => await HubconController.Pipeline.HandleWithoutResultAsync(this, info);
        public async Task ReceiveStream(string code, ChannelReader<object> reader) 
            => await StreamNotificationHandler.NotifyStream(code, reader);
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest info) 
            => HubconController.Pipeline.GetStream(this, info);

        public void Build(WebApplication? app)
        {
            HubconController.Pipeline.RegisterMethods(GetType());
            ClientReferences.TryAdd(GetType(), new());         
        }

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

        private IClientAccessor _clientAccessor = null!;

        private IClientAccessor ClientAccessor 
        { 
            get
            {
                if (_clientAccessor == null)
                {
                    Type clientManagerType = typeof(IClientAccessor<,>).MakeGenericType(typeof(TICommunicationContract), GetType());
                    _clientAccessor = (IClientAccessor)ServiceProvider.Resolve(clientManagerType);
                    
                }

                return _clientAccessor;
            } 
        }

        protected TICommunicationContract Client => ClientRegistry.TryGetClient<TICommunicationContract>(GetType(), Context.ConnectionId)!;    
        protected TICommunicationContract GetClient(string connectionId) => ClientRegistry.TryGetClient<TICommunicationContract>(GetType(), connectionId)!;


        [HubconInject]
        private ClientRegistry ClientRegistry { get; } = null!;

        public override Task OnConnectedAsync()
        {
            var client = ClientAccessor.GetClient<TICommunicationContract>(Context.ConnectionId);
            ClientRegistry.RegisterClient(GetType(), Context.ConnectionId, client);

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ClientRegistry.UnregisterClient(GetType(), Context.ConnectionId);

            return base.OnDisconnectedAsync(exception);
        }
    }
}
