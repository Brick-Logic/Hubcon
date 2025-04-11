using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Converters;
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
using Newtonsoft.Json.Linq;
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
                    if(prop.GetValue(e.Instance) != null)
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

        public static WebApplicationBuilder AddHubcon(this WebApplicationBuilder builder, Action<ContainerBuilder>? additionalServices = null, IServiceCollection? serviceCollection = null)
            => AddHubcon(builder, serviceCollection, additionalServices);
        public static WebApplicationBuilder AddHubcon(this WebApplicationBuilder builder, IServiceCollection? serviceCollection = null, Action<ContainerBuilder>? additionalServices = null)
        {
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
            builder.Host.ConfigureContainer<ContainerBuilder>((context, container) =>
            {
                if (ServicesToInject.Count > 0)
                    ServicesToInject.ForEach(x => x.Invoke(container));

                if (serviceCollection != null)
                    container.Populate(serviceCollection);

                container
                    .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<StreamNotificationHandler>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<ClientRegistry>().AsSingleton())
                    .RegisterWithInjector(x => x.RegisterType<HubconServiceProvider>().As<IHubconServiceProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(ClientControllerConnectorInterceptor<,>)).AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<MethodInvokerProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(ClientControllerConnectorInterceptor<,>)).AsScoped())
                    .RegisterWithInjector(x => x.RegisterType<RequestPipeline>().AsScoped())
                    .RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconClientConnector<,>)).As(typeof(IClientAccessor<,>)).AsScoped());

                additionalServices?.Invoke(container);

                container.RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconControllerManager<>)).AsScoped());
            });

            return builder;
        }

        public static IServiceProvider CreateHubconServiceProvider(Action<ContainerBuilder>? additionalServices = null)
        {
            var container = new ContainerBuilder();
             
            // Registrás tus tipos
            container
                   .RegisterWithInjector(x => x.RegisterType<DynamicConverter>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MethodInvokerProvider>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<MiddlewareProvider>().As<IMiddlewareProvider>().AsSingleton())
                   .RegisterWithInjector(x => x.RegisterType<RequestPipeline>().AsSingleton());

            additionalServices?.Invoke(container);

            container.RegisterWithInjector(x => x.RegisterGeneric(typeof(HubconControllerManager<>)).AsSingleton());

            // Build del container
            var builtContainer = container.Build();

            var scope = builtContainer.BeginLifetimeScope();

            return new AutofacServiceProvider(scope);
        }

        public static WebApplicationBuilder AddHubconController<T>(this WebApplicationBuilder builder, Action<IPipelineOptions>? options = null)
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
