using System.Diagnostics.CodeAnalysis;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core;
using Hubcon.Server.Core.Cache;
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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
#pragma warning disable CS1591

namespace Hubcon.Server
{
    public class ServerBuilder
    {
        private static readonly AtomicPass _serverIsBuilt = new();
        private static readonly IOperationRegistry _operationRegistry = new OperationRegistry();
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

        internal void ConfigureServices(Action<IServiceCollection> services)
        {
            services.Invoke(Services);
        }

        internal ServerBuilder AddHubconServer(
            WebApplicationBuilder builder,
            params Action<IServiceCollection>?[] additionalServices)
        {
            if (!_serverIsBuilt.TryAcquirePass()) throw new HubconGenericException("The hubcon server cannot be built two times.");
            
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
            Services.AddScoped<IOperationContext>(x => OperationContextProvider.GetContext() ?? throw new InvalidOperationException("No active operation context. IOperationContext is only available inside the Hubcon pipeline."));
            Services.AddSingleton(_operationRegistry);
            Services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
            Services.AddSingleton<IConnectionSupervisor, ConnectionSupervisor>();
            Services.AddSingleton<IDynamicConverter, DynamicConverter>();
            Services.AddSingleton(_operationRegistry);
            Services.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
            Services.AddSingleton<IOperationConfigRegistry, OperationConfigRegistry>();
            Services.AddSingleton<IGlobalRateLimiterManager, GlobalRateLimiterManager>();
            Services.AddScoped<IRequestHandler, RequestHandler>();

            foreach (var services in additionalServices)
                services?.Invoke(Services);

            AddGlobalMiddleware<InternalRoutingMiddleware>();
            AddGlobalMiddleware<InternalExceptionMiddleware>();

            builder.Services.AddHttpContextAccessor();

            return this;
        }

        internal void AddTransport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : HubconTransportAttribute, new()
        {
            ServerOptions.AddTransport<T>();
        }

        internal void AddTransport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T attribute) where T : HubconTransportAttribute
        {
            ServerOptions.AddTransport(attribute);
        }

        internal WebApplicationBuilder AddHubconController<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] T>(WebApplicationBuilder builder, Action<IControllerOptions>? options = null)
            where T : class, IControllerContract
        {
            var controllerType = typeof(T);
            
            List<Type> implementationTypes = controllerType
                .GetInterfaces()
                .Where(x => typeof(IControllerContract).IsAssignableFrom(x))
                .ToList();

            if (implementationTypes.Count == 0)
                throw new InvalidOperationException($"Class {controllerType.Name} does not implement interface {nameof(IControllerContract)}.");

            if (_operationRegistry.ControllerExists(controllerType))
                throw new InvalidOperationException($"Controller {controllerType.Name} has already been registered.");
            
            _operationRegistry.RegisterOperations(controllerType, options, ServerOptions, out var services);

            foreach (var service in services)
            {
                service.Invoke(Services);
            }

            return builder;
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

            if (_operationRegistry.ControllerExists(controllerType))
                throw new InvalidOperationException($"Controller {controllerType.Name} has already been registered.");

            _operationRegistry.RegisterOperations(controllerType, options, ServerOptions, out var services);

            foreach (var service in services)
            {
                service.Invoke(Services);
            }
            
            return builder;
        }

        internal void AddGlobalMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMiddleware>(Action<IServiceCollection, Type>? registerer = null)
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

        internal void AddTokenValidationParameters(TokenValidationParameters tokenValidationParameters)
        {
            ServerOptions.SetTokenValidationParameters(tokenValidationParameters);
        }
    }
}