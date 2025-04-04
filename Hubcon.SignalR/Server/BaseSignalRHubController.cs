using Hubcon.Core.Controllers;
using Hubcon.Core.Handlers;
using Hubcon.Core.Interfaces;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
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
            HubconController.Methods.BuildMethods(this, GetType());

            ClientReferences.TryAdd(GetType(), new());
        }

        public async Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info) 
            => await HubconController.Methods.HandleWithResultAsync(info);
        public async Task HandleMethodVoid(MethodInvokeRequest info) 
            => await HubconController.Methods.HandleWithoutResultAsync(info);
        public async Task ReceiveStream(string code, ChannelReader<object> reader) 
            => await StreamHandler.NotifyStream(code, reader);
        public IAsyncEnumerable<object> HandleMethodStream(MethodInvokeRequest info) 
            => HubconController.Methods.GetStream(info);

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

        private IClientManager _clientManager;
        protected IClientManager clientManager 
        { 
            get
            {
                if (_clientManager == null)
                {
                    Type clientManagerType = typeof(IClientManager<,>).MakeGenericType(typeof(TICommunicationContract), GetType());
                    using (var scope = StaticServiceProvider.Services.CreateScope())
                    {
                        var scopedProvider = scope.ServiceProvider;
                        _clientManager = (IClientManager)scopedProvider.GetRequiredService(clientManagerType);
                    }
                }

                return _clientManager;
            } 
        }
        protected TICommunicationContract? CurrentClient { get => clientManager.GetClient<TICommunicationContract>(Context.ConnectionId); }
        protected TICommunicationContract? GetClient(string connectionId) => clientManager.GetClient<TICommunicationContract>(connectionId);
    }
}
