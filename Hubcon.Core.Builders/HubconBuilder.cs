using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Authentication;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Extensions;
using Hubcon.Core.Injectors;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Invocation;
using Hubcon.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Core.Pipelines;
using Hubcon.Core.Pipelines.UpgradedPipeline;
using Hubcon.Core.Routing.MethodHandling;
using Hubcon.Core.Routing.Registries;
using Hubcon.Core.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hubcon.Core.Builders
{
    public class HubconBuilder
    {
        private IProxyRegistry Proxies { get; } = new ProxyRegistry();
        private ILiveSubscriptionRegistry SubscriptionRegistry { get; } = new LiveSubscriptionRegistry();
        private IOperationRegistry OperationRegistry { get; } = new OperationRegistry();
        private List<Action<ContainerBuilder>> ServicesToInject { get; } = new();
        private List<Type> ProxiesToRegister { get; } = new();


        private static HubconBuilder _current = null!;
        public static HubconBuilder Current
        {
            get
            {
                _current ??= new HubconBuilder();
                return _current;
            }
        }

        private HubconBuilder()
        {              
        }

        public HubconBuilder AddHubconServer(
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
                    .RegisterWithInjector(x => x.RegisterInstance(OperationRegistry).As<IOperationRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterInstance(Proxies).As<IProxyRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterInstance(SubscriptionRegistry).As<ILiveSubscriptionRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<InternalRoutingMiddleware>().As<IInternalRoutingMiddleware>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().As<IDynamicConverter>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<MethodDescriptorProvider>().As<IMethodDescriptorProvider>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<StreamNotificationHandler>().As<IStreamNotificationHandler>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<ClientRegistry>().As<IClientRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<HubconServiceProvider>().As<IHubconServiceProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterType(typeof(ClientControllerConnectorInterceptor)).As<IClientControllerConnectorInterceptor>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<RequestHandler>().As<IRequestHandler>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconClientConnector<>)).As(typeof(IClientAccessor<>)).AsScoped());

                foreach (var services in additionalServices)
                    services?.Invoke(container);
            });

            AddGlobalMiddleware<InternalRoutingMiddleware>(); 
            AddGlobalMiddleware<InternalExceptionMiddleware>(); 

            builder.Services.AddHttpContextAccessor();

            return this;
        }

        private bool AuthManagerIsRegistered { get; set; }

        public void UseAuthenticationManager<T>() where T : IAuthenticationManager
            => UseAuthenticationManager(typeof(T));
        public void UseAuthenticationManager(Type authenticationManagerType)
        {
            if(AuthManagerIsRegistered)
                return;

            ServicesToInject.Add(container => container
                .RegisterWithInjector(x => x
                    .RegisterType(authenticationManagerType)
                    .As<IAuthenticationManager>()
                    .AsSingleton()
            ));

            AuthManagerIsRegistered = true;
        }

        public ContainerBuilder AddHubconControllersFromAssembly(ContainerBuilder container, Assembly assembly, Action<IMiddlewareOptions>? globalMiddlewareOptions = null)
        {
            var contracts = assembly
                .GetTypes()
                .Where(t => t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t))
                .ToList();

            var controllers = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.IsDefined(typeof(HubconControllerAttribute)))
                .ToList();

            foreach (var controller in controllers)
                container.RegisterWithInjector(x => x.RegisterType(controller));

            return container;
        }

        public ContainerBuilder AddHubconEntrypoint(ContainerBuilder container, Type hubconEntrypointType)
        {
            if (!hubconEntrypointType.IsAssignableTo(typeof(IHubconEntrypoint)))
                throw new ArgumentException($"El tipo {hubconEntrypointType.Name} no implementa la interfaz {nameof(IHubconEntrypoint)}");

            return container.RegisterWithInjector(x => x.RegisterType(hubconEntrypointType));
        }

        public WebApplicationBuilder AddHubconController<T>(WebApplicationBuilder builder, Action<IMiddlewareOptions>? options = null) 
            where T : class, IControllerContract
        {
            return AddHubconController(builder, typeof(T), options);
        }

        public WebApplicationBuilder AddHubconController(
            WebApplicationBuilder builder,
            Type controllerType,
            Action<IMiddlewareOptions>? options = null)
        {
            List<Type> implementationTypes = controllerType
                .GetInterfaces()
                .Where(x => typeof(IControllerContract).IsAssignableFrom(x))
                .ToList();

            if (implementationTypes.Count == 0)
                throw new InvalidOperationException($"Class {controllerType.Name} does not implement interface {nameof(IControllerContract)}.");

            foreach (var type in implementationTypes)
            {
                foreach (var property in type.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))))
                {
                    var controllerProp = controllerType.GetProperty(property.Name);

                    SubscriptionRegistry.RegisterSubscriptionMetadata(property.ReflectedType!.Name, property.Name, controllerProp!);
                }
            }

            OperationRegistry.RegisterOperations(controllerType, options, out var services);
            ServicesToInject.AddRange(services);

            return builder;
        }

        public void AddGlobalMiddleware<TMiddleware>() => AddGlobalMiddleware(typeof(TMiddleware));
        public void AddGlobalMiddleware(Type middlewareType)
        {
            if (!middlewareType.IsAssignableTo(typeof(IMiddleware)))
                throw new ArgumentException($"El tipo {middlewareType.Name} no implementa la interfaz {nameof(IMiddleware)}");

            PipelineBuilder.AddglobalMiddleware(middlewareType);

            ServicesToInject.Add(container => container
                .RegisterWithInjector(x => x
                    .RegisterType(middlewareType)
                        .IfNotRegistered(middlewareType)));
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
                .Where(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.IsDefined(typeof(HubconProxyAttribute)))
                .ToList();

            foreach (var contract in contracts)
            {
                var proxy = proxies.Find(x => x.Name == contract.Name + "Proxy")!;
                Proxies.RegisterProxy(contract, proxy);
                ProxiesToRegister.Add(proxy);
            }

            return e;
        }

        public IServiceProvider AddContractsFromAssembly(IServiceProvider serviceProvider, string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);
            return AddContractsFromAssembly(serviceProvider, assembly);
        }

        public IServiceProvider AddContractsFromAssembly(IServiceProvider serviceProvider, Assembly assembly)
        {
            var proxyRegistry = serviceProvider.GetRequiredService<IProxyRegistry>();

            var contracts = assembly
                .GetTypes()
                .Where(t => t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t))
                .ToList();

            var classes = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t))
                .ToList();

            foreach (var contract in contracts)
            {
                proxyRegistry.RegisterProxy(contract, classes.Find(x => x.Name == contract.Name + "Proxy")!);
            }

            return serviceProvider;
        }

        public IServiceProvider CreateHubconServiceProvider(
            IBaseHubconController iBaseHubconControllerInstance,
            Action<ContainerBuilder>? additionalServices = null,
            Action<IMiddlewareOptions>? options = null)
        {
            var container = new ContainerBuilder();

            container
                   .RegisterWithInjector(x => x.RegisterInstance(Proxies).AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MethodDescriptorProvider>().As<IMethodDescriptorProvider>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().As<IDynamicConverter>().AsSingleton())
                   //.RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                   .RegisterWithInjector(x => x.RegisterType<RequestHandler>().As<IRequestHandler>().AsScoped())
                   .RegisterWithInjector(x => x.RegisterType(typeof(HubconControllerManager)).As(typeof(IHubconControllerManager)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterType(typeof(ServerConnectorInterceptor)).As(typeof(IContractInterceptor)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconServerConnector<>)).As(typeof(IHubconServerConnector<>)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterInstance(iBaseHubconControllerInstance).As(iBaseHubconControllerInstance.GetType()).AsSingleton());

            additionalServices?.Invoke(container);

            if (options != null)
            {
                //MiddlewareProvider.AddMiddlewares(iBaseHubconControllerInstance.GetType(), options, GlobalMiddlewares, ServicesToInject);

                foreach (var service in ServicesToInject)
                    ServicesToInject.ForEach(x => x.Invoke(container));
            }

            // Build del container
            var builtContainer = container.Build();

            var scope = builtContainer.BeginLifetimeScope();

            return new AutofacServiceProvider(scope);
        }
    }
}
