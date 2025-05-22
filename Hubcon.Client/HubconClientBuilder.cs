using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Extensions;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Pipelines;
using Hubcon.Core.Routing.MethodHandling;
using Hubcon.Core.Routing.Registries;
using Hubcon.Core.Serialization;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Client
{
    public class HubconClientBuilder
    {
        private ProxyRegistry Proxies { get; }
        private ClientBuilderRegistry ClientBuilders { get; }
        private List<Action<ContainerBuilder>> ServicesToInject { get; } = new();
        private List<Type> ProxiesToRegister { get; } = new();

        private HubconClientBuilder()
        {
            Proxies = new();
            ClientBuilders = new ClientBuilderRegistry(Proxies);
        }

        private static HubconClientBuilder _current = null!;
        public static HubconClientBuilder Current
        {
            get
            {
                _current ??= new HubconClientBuilder();
                return _current;
            }
        }

        public void AddService(Action<ContainerBuilder> container)
        {
            ServicesToInject.Add(container);
        }

        public WebApplicationBuilder AddHubconClientServices(
            WebApplicationBuilder builder,
            params Action<ContainerBuilder>?[] additionalServices)
        {
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            builder.Host.ConfigureContainer<ContainerBuilder>((context, container) =>
            {
                if (ServicesToInject.Count > 0)
                    ServicesToInject.ForEach(x => x.Invoke(container));

                if (ProxiesToRegister.Count > 0)
                    ProxiesToRegister.ForEach(x => container.RegisterWithInjector(y => y.RegisterType(x).AsTransient()));

                container
                       .RegisterWithInjector(x => x.RegisterInstance(Proxies).As<IProxyRegistry>().AsSingleton())
                       .RegisterWithInjector(x => x.RegisterInstance(ClientBuilders).As<IClientBuilderRegistry>().AsSingleton())
                       .RegisterWithInjector(x => x.RegisterType<MethodDescriptorProvider>().As<IMethodDescriptorProvider>().AsSingleton())
                       .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().As<IDynamicConverter>().AsSingleton())
                       .RegisterWithInjector(x => x.RegisterType<RequestHandler>().As<IRequestHandler>().AsScoped())
                       .RegisterWithInjector(x => x.RegisterType(typeof(HubconControllerManager)).As(typeof(IHubconControllerManager)).AsScoped())
                       .RegisterWithInjector(x => x.RegisterType(typeof(ServerConnectorInterceptor)).As(typeof(IContractInterceptor)).AsScoped())
                       .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconServerConnector<>)).As(typeof(IHubconServerConnector<>)).AsScoped());

                foreach (var services in additionalServices)
                    services?.Invoke(container);
            });

            return builder;
        }

        public WebApplicationBuilder AddRemoteServerModule<TRemoteServerModule>(WebApplicationBuilder builder)
             where TRemoteServerModule : IRemoteServerModule, new()
        {
            ClientBuilders.RegisterModule<TRemoteServerModule>(ServicesToInject);

            return builder;
        }

        public void LoadContractProxy(Type contractType)
        {
            if (!contractType.IsAssignableTo(typeof(IControllerContract)))
                return;

            var proxyTypes = contractType.Assembly
                .GetTypes()
                .Where(t => !t.IsInterface
                    && typeof(IControllerContract).IsAssignableFrom(t)
                    && t.IsDefined(typeof(HubconProxyAttribute), inherit: false))
                .ToList();

            var proxy = proxyTypes.Find(x => x.Name == contractType.Name + "Proxy")!;
            proxyTypes.Add(proxy);

            Proxies.RegisterProxy(contractType, proxy);
            ProxiesToRegister.Add(proxy);
        }

        public WebApplicationBuilder UseContractsFromAssembly(WebApplicationBuilder e, string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);

            var contracts = assembly
                .GetTypes()
                .Where(t => t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t))
                .ToList();

            var proxies = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.IsDefined(typeof(HubconProxyAttribute), inherit:true))
                .ToList();

            foreach (var contract in contracts)
            {
                var proxy = proxies.Find(x => x.Name == contract.Name + "Proxy")!;
                Proxies.RegisterProxy(contract, proxy);
                ProxiesToRegister.Add(proxy);
            }

            return e;
        }
    }
}
