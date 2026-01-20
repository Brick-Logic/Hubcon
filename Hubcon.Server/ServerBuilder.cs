using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Server.Core.RateLimiting;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Server.Core.Supervisor;
using Hubcon.Server.Core.Telemetry;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Injection;
using Hubcon.Shared.Core.Serialization;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server
{
    public class ServerBuilder
    {
        private ILiveSubscriptionRegistry SubscriptionRegistry { get; } = new LiveSubscriptionRegistry();
        private IOperationRegistry OperationRegistry { get; } = new OperationRegistry();
        private CoreServerOptions ServerOptions { get; } = new();
        private IServiceCollection Services;


        private static ServerBuilder _current = null!;
        public static ServerBuilder Current
        {
            get
            {
                _current ??= new ServerBuilder();
                return _current;
            }
        }

        private ServerBuilder()
        {
        }

        internal ServerBuilder AddHubconServer(
            WebApplicationBuilder builder,
            params Action<IServiceCollection>?[] additionalServices)
        {
            Services = builder.Services;

            builder.AddServerCore();

            Services.AddSingleton<IInternalServerOptions>(ServerOptions);
            Services.AddSingleton(OperationRegistry);
            Services.AddSingleton(SubscriptionRegistry);
            Services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
            Services.AddSingleton<IConnectionSupervisor, ConnectionSupervisor>();
            Services.AddSingleton<IDynamicConverter, DynamicConverter>();
            Services.AddSingleton(OperationRegistry);
            Services.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
            Services.AddScoped<ISettingsManager, SettingsManager>();
            Services.AddScoped<IOperationConfigRegistry, OperationConfigRegistry>();
            Services.AddScoped<IRateLimiterManager, RateLimiterManager>();
            Services.AddScoped<IRequestHandler, RequestHandler>();

            foreach (var services in additionalServices)
                services?.Invoke(Services);

            AddGlobalMiddleware<InternalRoutingMiddleware>();
            AddGlobalMiddleware<InternalExceptionMiddleware>();

            builder.Services.AddHttpContextAccessor();

            return this;
        }

        //internal ContainerBuilder AddHubconControllersFromAssembly(ContainerBuilder container, Assembly assembly, Action<IControllerOptions>? globalMiddlewareOptions = null)
        //{
        //    var contracts = assembly
        //        .GetTypes()
        //        .Where(t => t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t))
        //        .ToList();

        //    var controllers = assembly
        //        .GetTypes()
        //        .Where(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.IsDefined(typeof(HubconControllerAttribute)))
        //        .ToList();

        //    foreach (var controller in controllers)
        //        container.RegisterWithInjector(x => x.RegisterType(controller));

        //    return container;
        //}

        //internal ContainerBuilder AddHubconEntrypoint(ContainerBuilder container, Type hubconEntrypointType)
        //{
        //    if (!hubconEntrypointType.IsAssignableTo(typeof(DefaultEntrypoint)))
        //        throw new ArgumentException($"El tipo {hubconEntrypointType.Name} no es compatible con la clase {nameof(DefaultEntrypoint)}");

        //    return container.RegisterWithInjector(x => x.RegisterType(hubconEntrypointType));
        //}

        internal WebApplicationBuilder AddHubconController<T>(WebApplicationBuilder builder, Action<IControllerOptions>? options = null)
            where T : class, IControllerContract
        {
            return AddHubconController(builder, typeof(T), options);
        }

        internal WebApplicationBuilder AddHubconController(
            WebApplicationBuilder builder,
            Type controllerType,
            Action<IControllerOptions>? options = null)
        {
            List<Type> implementationTypes = controllerType
                .GetInterfaces()
                .Where(x => typeof(IControllerContract).IsAssignableFrom(x))
                .ToList();

            if (implementationTypes.Count == 0)
                throw new InvalidOperationException($"Class {controllerType.Name} does not implement interface {nameof(IControllerContract)}.");

            if (OperationRegistry.ControllerExists(controllerType))
                return builder;

            foreach (var type in implementationTypes)
            {
                foreach (var property in type.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))))
                {
                    var controllerProp = controllerType.GetProperty(property.Name);

                    SubscriptionRegistry.RegisterSubscriptionMetadata(NamingHelper.GetCleanName(property.DeclaringType!.Name), property.Name, controllerProp!);
                }
            }

            OperationRegistry.RegisterOperations(controllerType, options, ServerOptions, out var services);

            foreach (var service in services)
                service.Invoke(Services);

            return builder;
        }

        internal void AddGlobalMiddleware<TMiddleware>(Action<IServiceCollection, Type>? registerer = null)
        {
            var middlewareType = typeof(TMiddleware);

            if (!middlewareType.IsAssignableTo(typeof(Abstractions.Interfaces.IMiddleware)))
                throw new ArgumentException($"El tipo {middlewareType.Name} no implementa la interfaz {nameof(Abstractions.Interfaces.IMiddleware)}");

            PipelineBuilder.AddGlobalMiddleware(middlewareType);

            if (registerer == null)
                Services.AddScoped(middlewareType);
            else
                registerer.Invoke(Services, middlewareType);
        }

        internal void AddGlobalMiddleware(Type middlewareType, Action<IServiceCollection, Type>? registerer = null)
        {
            if (!middlewareType.IsAssignableTo(typeof(Abstractions.Interfaces.IMiddleware)))
                throw new ArgumentException($"El tipo {middlewareType.Name} no implementa la interfaz {nameof(Abstractions.Interfaces.IMiddleware)}");

            PipelineBuilder.AddGlobalMiddleware(middlewareType);

            if (registerer == null)
                Services.AddScoped(middlewareType);
            else
                registerer.Invoke(Services, middlewareType);
        }

        internal void ConfigureCore(Action<ICoreServerOptions> coreServerOptions)
        {
            coreServerOptions.Invoke(ServerOptions);
        }
    }
}