using Autofac;
using Autofac.Core;
using Castle.DynamicProxy;
using Hubcon.Core.Injectors;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Registries;
using Hubcon.Core.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Core.Connectors
{
    public interface IServerConnector
    {
        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }

    /// <summary>
    /// The ServerHubConnector allows a client to connect itself to a ServerHub and control its methods given its URL and
    /// the server's interface.
    /// </summary>
    /// <typeparam name="TIServerHubController"></typeparam>
    public class HubconServerConnector<TICommunicationHandler> : IServerConnector, IHubconServerConnector<TICommunicationHandler>
        where TICommunicationHandler : ICommunicationHandler
    {
        private IControllerContract? _client = null!;
        private readonly IServerConnectorInterceptor<TICommunicationHandler> Interceptor;
        private readonly IProxyRegistry proxyRegistry;

        public ICommunicationHandler Connection { get => Interceptor.CommunicationHandler; }

        private ILifetimeScope _lifetimeScope { get; }

        public HubconServerConnector(
            IServerConnectorInterceptor<TICommunicationHandler> interceptor,
            IProxyRegistry proxyRegistry,
            ILifetimeScope lifetimeScoped) : base()
        {
            Interceptor = interceptor;
            this.proxyRegistry = proxyRegistry;
            _lifetimeScope = lifetimeScoped;
        }

        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract
        {
            if (_client != null)
                return (TICommunicationContract)_client;

            var proxyType = proxyRegistry.TryGetProxy<TICommunicationContract>();
            _client = (TICommunicationContract)_lifetimeScope.Resolve(proxyType, new[]
            {
                new TypedParameter(typeof(AsyncInterceptorBase), Interceptor)
            });

            return (TICommunicationContract)_client;
        }
    }
}
