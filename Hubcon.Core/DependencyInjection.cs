using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Converters;
using Hubcon.Core.Dummy;
using Hubcon.Core.Handlers;
using Hubcon.Core.Injectors;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.Interceptors;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Middleware;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Models.Pipeline.Interfaces;
using Hubcon.Core.Registries;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Reflection.Metadata;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Hubcon.Core
{
    public static class DependencyInjection
    {
        public static ContainerBuilder RegisterWithInjector<TType, TActivatorData, TSingleRegistrationStyle>(
            this ContainerBuilder container,
            Func<ContainerBuilder, IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle>>? options = null)
        {
            var registered = options?.Invoke(container);
            registered?.OnActivated(e =>
            {
                //Console.WriteLine($"Instancia creada: {e?.Instance?.GetType().Name}");

                List<PropertyInfo> props = new();

                props.AddRange(e.Instance!.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(prop => Attribute.IsDefined(prop, typeof(HubconInjectAttribute)))
                    .ToList());

                props.AddRange(e.Instance!.GetType().BaseType!
                    .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(p => p.IsDefined(typeof(HubconInjectAttribute), false))
                    .ToList());

                foreach (PropertyInfo prop in props!)
                {
                    if (prop.GetValue(e.Instance) != null)
                        continue;

                    var resolved = e.Context.ResolveOptional(prop.PropertyType);

                    if (resolved == null)
                        continue;

                    var instance = e.Instance;

                    var setMethod = prop!.GetSetMethod(true);
                    if (setMethod != null)
                    {
                        setMethod.Invoke(instance, new[] { resolved });
                    }
                    else
                    {
                        // Si no tiene setter, usamos el campo backing
                        var field = prop!.DeclaringType?.GetField($"<{prop.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);

                        field?.SetValue(instance, resolved);
                    }
                }
            });

            return container;
        }

        public static IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> AsScoped<TType, TActivatorData, TSingleRegistrationStyle>(this IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> regBuilder)
            => regBuilder.InstancePerLifetimeScope();

        public static IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> AsTransient<TType, TActivatorData, TSingleRegistrationStyle>(this IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> regBuilder)
            => regBuilder.InstancePerDependency();

        public static IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> AsSingleton<TType, TActivatorData, TSingleRegistrationStyle>(this IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> regBuilder)
            => regBuilder.SingleInstance();

        private static List<Action<ContainerBuilder>> ServicesToInject { get; } = new();

        public static WebApplicationBuilder AddHubcon(
            this WebApplicationBuilder builder,
            params Action<ContainerBuilder>?[] additionalServices)
        {
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            builder.Host.ConfigureContainer<ContainerBuilder>((context, container) =>
            {
                if (ServicesToInject.Count > 0)
                    ServicesToInject.ForEach(x => x.Invoke(container));

                container
                    .RegisterWithInjector(x => x.RegisterInstance(Proxies).AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<DummyCommunicationHandler>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<DummyServerCommunicationHandler>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<MethodInvokerProvider>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<StreamNotificationHandler>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<ClientRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<HubconServiceProvider>().As<IHubconServiceProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(ClientControllerConnectorInterceptor<,>)).AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(ClientControllerConnectorInterceptor<,>)).AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<RequestPipeline>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconClientConnector<,>)).As(typeof(IClientAccessor<,>)).AsScoped());

                foreach(var services in additionalServices)
                    services?.Invoke(container);
            });

            return builder;
        }

        private readonly static ProxyRegistry Proxies = new();

        public static WebApplicationBuilder AddContractsFromAssembly(this WebApplicationBuilder e, string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);

            var contracts = assembly
                .GetTypes()
                .Where(t => t.IsInterface && typeof(ICommunicationContract).IsAssignableFrom(t))
                .ToList();

            var classes = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && typeof(ICommunicationContract).IsAssignableFrom(t))
                .ToList();

            foreach (var contract in contracts)
                Proxies.RegisterProxy(contract, classes.Find(x => x.Name == contract.Name + "Proxy")!);

            return e;
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);
            return AddContractsFromAssembly(serviceProvider, assembly);
        }

        public static IServiceProvider AddContractsFromAssembly(this IServiceProvider serviceProvider, Assembly assembly)
        {
            var proxyRegistry = serviceProvider.GetRequiredService<ProxyRegistry>();

            var contracts = assembly
                .GetTypes()
                .Where(t => t.IsInterface && typeof(ICommunicationContract).IsAssignableFrom(t))
                .ToList();

            var classes = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && typeof(ICommunicationContract).IsAssignableFrom(t))
                .ToList();

            foreach(var contract in contracts)
                proxyRegistry.RegisterProxy(contract, classes.Find(x => x.Name == contract.Name + "Proxy")!);

            return serviceProvider;
        }

        public static IServiceProvider CreateHubconServiceProvider<TICommunicationHandler>(
            IBaseHubconController<TICommunicationHandler> iBaseHubconControllerInstance,
            Action<ContainerBuilder>? additionalServices = null, 
            Action<IMiddlewareOptions>? options = null) where TICommunicationHandler : ICommunicationHandler
        {
            var container = new ContainerBuilder();

            // Registrás tus tipos
            container
                   .RegisterWithInjector(x => x.RegisterInstance(Proxies).AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MethodInvokerProvider>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                   .RegisterWithInjector(x => x.RegisterType<RequestPipeline>().AsScoped())
                   .RegisterWithInjector(x => x.RegisterType(typeof(TICommunicationHandler)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterType(typeof(HubconControllerManager<TICommunicationHandler>)).As(typeof(IHubconControllerManager)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterGeneric(typeof(ServerConnectorInterceptor<,>)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconServerConnector<,>)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconControllerManager<>)).AsScoped())
                   .RegisterWithInjector(x => x.RegisterInstance(iBaseHubconControllerInstance).As(iBaseHubconControllerInstance.GetType()).AsSingleton());

            additionalServices?.Invoke(container);

            if(options != null)
            {
                MiddlewareProvider.AddMiddlewares(iBaseHubconControllerInstance.GetType(), options, ServicesToInject);

                foreach (var service in ServicesToInject)
                    ServicesToInject.ForEach(x => x.Invoke(container));
            }

            container.RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconControllerManager<>)).AsSingleton());

            // Build del container
            var builtContainer = container.Build();

            var scope = builtContainer.BeginLifetimeScope();

            return new AutofacServiceProvider(scope);
        }

        public static WebApplicationBuilder AddHubconController<T>(
            this WebApplicationBuilder builder,
            Action<IMiddlewareOptions>? options = null)
            where T : class, IBaseHubconController, ICommunicationContract
        {
            var controllerType = typeof(T);
            List<Type> implementationTypes = controllerType
                .GetInterfaces()
                .Where(x => typeof(ICommunicationContract).IsAssignableFrom(x))
                .ToList();

            if (implementationTypes.Count == 0)
                throw new InvalidOperationException($"Controller {controllerType.Name} does not implement ICommunicationContract.");


            Action<ContainerBuilder> injector = (ContainerBuilder x) => x.RegisterWithInjector(x => x.RegisterType<T>().AsScoped());

            ServicesToInject.Add(injector);

            if (options != null)
            {
                MiddlewareProvider.AddMiddlewares<T>(options, ServicesToInject);
            }

            return builder;
        }
    }
}
