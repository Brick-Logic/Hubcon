using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Registries;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Injectors;
using Hubcon.Server.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Server.Core.Routing.MethodHandling;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Server.Interceptors;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hubcon.Server
{
    public class HubconServerBuilder
    {
        private IProxyRegistry Proxies { get; } = new ProxyRegistry();
        private ILiveSubscriptionRegistry SubscriptionRegistry { get; } = new LiveSubscriptionRegistry();
        private IOperationRegistry OperationRegistry { get; } = new OperationRegistry();
        private List<Action<ContainerBuilder>> ServicesToInject { get; } = new();
        private List<Type> ProxiesToRegister { get; } = new();


        private static HubconServerBuilder _current = null!;
        public static HubconServerBuilder Current
        {
            get
            {
                _current ??= new HubconServerBuilder();
                return _current;
            }
        }

        private HubconServerBuilder()
        {              
        }

        public HubconServerBuilder AddHubconServer(
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
                    .RegisterWithInjector(x => x.RegisterType<RequestHandler>().As<IRequestHandler>().AsScoped());
                    //.RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconClientConnector<>)).As(typeof(IClientAccessor<>)).AsScoped());

                foreach (var services in additionalServices)
                    services?.Invoke(container);
            });

            AddGlobalMiddleware<InternalRoutingMiddleware>(); 
            AddGlobalMiddleware<InternalExceptionMiddleware>(); 

            builder.Services.AddHttpContextAccessor();

            return this;
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
    }
}
