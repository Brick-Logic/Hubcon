using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Extensions;
using Hubcon.Core.Injectors;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Invocation;
using Hubcon.Core.Middlewares;
using Hubcon.Core.Pipelines;
using Hubcon.Core.Routing.MethodHandling;
using Hubcon.Core.Routing.Registries;
using Hubcon.Core.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hubcon.Core.Builders
{
    public static class DependencyInjection
    {
        private static List<Action<ContainerBuilder>> ServicesToInject { get; } = new();
        private static List<Action<IMiddlewareOptions>> GlobalMiddlewares { get; } = new();

        private readonly static IProxyRegistry Proxies = new ProxyRegistry();
        private readonly static ISubscriptionRegistry SubscriptionRegistry = new SubscriptionRegistry();
        private readonly static List<Type> ProxiesToRegister = new();
        private readonly static List<Type> ControllersToRegister = new();

        public static WebApplicationBuilder AddHubconServer(
            this WebApplicationBuilder builder,
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
                    .RegisterWithInjector(x => x.RegisterInstance(SubscriptionRegistry).As<ISubscriptionRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().As<IDynamicConverter>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<MethodDescriptorProvider>().As<IMethodDescriptorProvider>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<StreamNotificationHandler>().As<IStreamNotificationHandler>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<ClientRegistry>().As<IClientRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<HubconServiceProvider>().As<IHubconServiceProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterType(typeof(ClientControllerConnectorInterceptor)).As<IClientControllerConnectorInterceptor>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<ControllerInvocationHandler>().As<IControllerInvocationHandler>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconClientConnector<>)).As(typeof(IClientAccessor<>)).AsScoped());

                foreach(var services in additionalServices)
                    services?.Invoke(container);
            });

            return builder;
        }

        public static void MapHubconControllers(this WebApplication app)
        {
            var invokerProvider = app.Services.GetRequiredService<IMethodDescriptorProvider>();

            foreach(var controller in ControllersToRegister)
            {
                invokerProvider.RegisterMethods(controller);
            }
        }

        public static ContainerBuilder AddHubconControllersFromAssembly(this ContainerBuilder container, Assembly assembly, Action<IMiddlewareOptions>? globalMiddlewareOptions = null)
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

            if(globalMiddlewareOptions != null)
                GlobalMiddlewares.Add(globalMiddlewareOptions);

            return container;
        }

        public static WebApplicationBuilder UseContractsFromAssembly(this WebApplicationBuilder e, string assemblyName)
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

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);
            return serviceProvider.AddContractsFromAssembly(assembly);
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, Assembly assembly)
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

            foreach(var contract in contracts)
            {
                proxyRegistry.RegisterProxy(contract, classes.Find(x => x.Name == contract.Name + "Proxy")!);
            }

            return serviceProvider;
        }

        public static IServiceProvider CreateHubconServiceProvider(
            IBaseHubconController iBaseHubconControllerInstance,
            Action<ContainerBuilder>? additionalServices = null, 
            Action<IMiddlewareOptions>? options = null)
        {
            var container = new ContainerBuilder();

            container
                   .RegisterWithInjector(x => x.RegisterInstance(Proxies).AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MethodDescriptorProvider>().As<IMethodDescriptorProvider>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().As<IDynamicConverter>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                   .RegisterWithInjector(x => x.RegisterType<ControllerInvocationHandler>().As<IControllerInvocationHandler>().AsScoped())
                   .RegisterWithInjector(x => x.RegisterType(typeof(HubconControllerManager)).As(typeof(IHubconControllerManager)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterGeneric(typeof(ServerConnectorInterceptor<>)).As(typeof(IServerConnectorInterceptor<>)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconServerConnector<>)).As(typeof(IHubconServerConnector<>)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterInstance(iBaseHubconControllerInstance).As(iBaseHubconControllerInstance.GetType()).AsSingleton());

            additionalServices?.Invoke(container);

            if(options != null)
            {
                MiddlewareProvider.AddMiddlewares(iBaseHubconControllerInstance.GetType(), options, GlobalMiddlewares, ServicesToInject);

                foreach (var service in ServicesToInject)
                    ServicesToInject.ForEach(x => x.Invoke(container));
            }

            // Build del container
            var builtContainer = container.Build();

            var scope = builtContainer.BeginLifetimeScope();

            return new AutofacServiceProvider(scope);
        }

        public static WebApplicationBuilder AddHubconClientServices(
            this WebApplicationBuilder builder,
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
                       .RegisterWithInjector(x => x.RegisterType<MethodDescriptorProvider>().As<IMethodDescriptorProvider>().AsSingleton())
                       .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().As<IDynamicConverter>().AsSingleton())
                       .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                       .RegisterWithInjector(x => x.RegisterType<ControllerInvocationHandler>().As<IControllerInvocationHandler>().AsScoped())
                       .RegisterWithInjector(x => x.RegisterType(typeof(HubconControllerManager)).As(typeof(IHubconControllerManager)).AsScoped())
                       .RegisterWithInjector(x => x.RegisterGeneric(typeof(ServerConnectorInterceptor<>)).As(typeof(IServerConnectorInterceptor<>)).AsScoped())
                       .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconServerConnector<>)).As(typeof(IHubconServerConnector<>)).AsScoped());

                foreach (var services in additionalServices)
                    services?.Invoke(container);
            });

            return builder;
        }

        public static WebApplicationBuilder AddHubconController<T>(this WebApplicationBuilder builder,Action<IMiddlewareOptions>? options = null)
            where T : class, IControllerContract
                => builder.AddHubconController(typeof(T), options);

        public static void AddGlobalMiddleware<TMiddleware>(this WebApplicationBuilder builder) => builder.AddGlobalMiddleware(typeof(TMiddleware));
        public static void AddGlobalMiddleware(this WebApplicationBuilder builder, Type middlewareType)
        {
            if (!middlewareType.IsAssignableTo(typeof(IMiddleware)))
                throw new ArgumentException($"El tipo {middlewareType.Name} no implementa la interfaz {nameof(IMiddleware)}");

            GlobalMiddlewares.Add(x => x.AddMiddleware(middlewareType));
        }

        public static ContainerBuilder AddHubconEntrypoint<THubconEntrypoint>(this ContainerBuilder container)
            where THubconEntrypoint : class, IHubconEntrypoint
                => container.AddHubconEntrypoint(typeof(THubconEntrypoint));

        public static ContainerBuilder AddHubconEntrypoint(this ContainerBuilder container, Type hubconEntrypointType)
        {
            if (!hubconEntrypointType.IsAssignableTo(typeof(IHubconEntrypoint)))
                throw new ArgumentException($"El tipo {hubconEntrypointType.Name} no implementa la interfaz {nameof(IHubconEntrypoint)}");

            return container.RegisterWithInjector(x => x.RegisterType(hubconEntrypointType));
        }

        public static WebApplicationBuilder AddHubconController(
            this WebApplicationBuilder builder,
            Type controllerType,
            Action<IMiddlewareOptions>? options = null)
        {
            List<Type> implementationTypes = controllerType
                .GetInterfaces()
                .Where(x => typeof(IControllerContract).IsAssignableFrom(x))
                .ToList();

            if (implementationTypes.Count == 0)
                throw new InvalidOperationException($"Class {controllerType.Name} does not implement interface {nameof(IControllerContract)}.");


            Action<ContainerBuilder> injector = (x) => x.RegisterWithInjector(x => x.RegisterType(controllerType).AsScoped());

            ServicesToInject.Add(injector);
            ControllersToRegister.Add(controllerType);
            
            foreach(var type in implementationTypes)
            {
                foreach(var property in type.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))))
                {
                    var controllerProp = controllerType.GetProperty(property.Name);

                    SubscriptionRegistry.RegisterSubscriptionMetadata(property.ReflectedType!.Name, property.Name, controllerProp!);
                }
            }

            if (options != null || GlobalMiddlewares.Count > 0)
            {
                MiddlewareProvider.AddMiddlewares(controllerType, options, GlobalMiddlewares, ServicesToInject);
            }

            return builder;
        }
    }
}