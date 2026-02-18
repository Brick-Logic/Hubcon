using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Server.Core.RateLimiting;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Server.Core.Supervisor;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Injection;
using Hubcon.Shared.Core.Serialization;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Builder;
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

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.WriteLine("UNHANDLED EXCEPTION:");
                Console.WriteLine(e.ExceptionObject);
                Console.WriteLine("Press two times to exit...");
                Console.ReadKey();
                Console.ReadKey();
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Console.WriteLine("UNOBSERVED TASK EXCEPTION:");
                Console.WriteLine(e.Exception);
                e.SetObserved();
            };

            builder.AddServerCore();

            ServerOptions.AddTransport<HttpTransport>();
            ServerOptions.AddTransport<WebSocketTransport>();

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
            AddGlobalMiddleware<InternalConcurrencyCheckMiddleware>((services, type) => services.AddSingleton(type));

            builder.Services.AddHttpContextAccessor();

            return this;
        }

        internal void AddTransport<T>() where T : HubconTransportAttribute, new()
        {
            ServerOptions.AddTransport<T>();
        }

        internal void AddTransport<T>(T attribute) where T : HubconTransportAttribute
        {
            ServerOptions.AddTransport(attribute);
        }

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

            //foreach (var type in implementationTypes)
            //{
            //    foreach (var property in type.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))))
            //    {
            //        var controllerProp = controllerType.GetProperty(property.Name);

            //        SubscriptionRegistry.RegisterSubscriptionMetadata(NamingHelper.GetCleanName(property.DeclaringType!.Name), property.Name, controllerProp!);
            //    }
            //}

            OperationRegistry.RegisterOperations(controllerType, options, ServerOptions, out var services);

            foreach (var service in services)
                service.Invoke(Services);

            return builder;
        }

        internal void AddGlobalMiddleware<TMiddleware>(Action<IServiceCollection, Type>? registerer = null)
        {
            var middlewareType = typeof(TMiddleware);

            if (!middlewareType.IsAssignableTo(typeof(IMiddleware)))
                throw new ArgumentException($"El tipo {middlewareType.Name} no implementa la interfaz {nameof(IMiddleware)}");

            PipelineBuilder.AddGlobalMiddleware(middlewareType);

            if (registerer == null)
                Services.AddScoped(middlewareType);
            else
                registerer.Invoke(Services, middlewareType);
        }

        internal void AddGlobalMiddleware(Type middlewareType, Action<IServiceCollection, Type>? registerer = null)
        {
            if (!middlewareType.IsAssignableTo(typeof(IMiddleware)))
                throw new ArgumentException($"El tipo {middlewareType.Name} no implementa la interfaz {nameof(IMiddleware)}");

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