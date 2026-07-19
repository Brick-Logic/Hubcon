using System.Diagnostics.CodeAnalysis;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Helpers;
using Hubcon.Server.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Server.Core.Security;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using Hubcon.Server.Core.EndpointManagement;

namespace Hubcon.Server.Injection
{
    internal sealed class DefaultServerOptions : IServerOptions
    {
        public DefaultServerOptions(WebApplicationBuilder builder, ServerBuilder hubconBuilder)
        {
            Builder = builder;
            HubconServerBuilder = hubconBuilder;
        }

        public WebApplicationBuilder Builder { get; }
        public ServerBuilder HubconServerBuilder { get; }

        public void ConfigureCore(Action<ICoreServerOptions> coreServerOptions)
        {
            HubconServerBuilder.ConfigureCore(coreServerOptions);
        }

        public void UseCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T: class, IOperationCache
        {
            HubconServerBuilder.ConfigureServices(services =>
            {
                services.TryAddSingleton<IOperationCache, T>();
            });
        }

        public void AddGlobalMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        {
            HubconServerBuilder.AddGlobalMiddleware<T>();
        }

        public void AddGlobalMiddleware(Type middlewareType)
        {
            HubconServerBuilder.AddGlobalMiddleware(middlewareType);
        }

        public void AddController<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] T>(Action<IControllerOptions>? options = null) where T : class, IControllerContract
        {
            HubconServerBuilder.AddHubconController<T>(Builder, options);
        }

        public void AddController(Type controllerType, Action<IControllerOptions>? options = null)
        {
            HubconServerBuilder.AddHubconController(Builder, controllerType, options);
        }

        public void AddAuthentication()
        {
            HubconServerBuilder.AddGlobalMiddleware<InternalAuthorizationMiddleware>((services, middleware) => services.TryAddSingleton(middleware));
        }

        public void AddTelemetry()
        {
            HubconServerBuilder.AddGlobalMiddleware<InternalTelemetryMiddleware>((x, y) => x.TryAddSingleton(y));
        }

        public void AutoRegisterControllers()
        {
            var foundControllers = ControllerMetadata.GetAvailableControllers();
            foreach (var controller in foundControllers)
            {
                HubconServerBuilder.AddHubconController(Builder, controller);
            }
        }

        public void RegisterControllersFromAssembly(Assembly assembly)
        {
            var foundControllers = ControllerContractHelper.FindImplementations(assembly, [typeof(BaseClientProxyMarker)]).ToList();
            foreach (var controller in foundControllers)
            {
                HubconServerBuilder.AddHubconController(Builder, controller);
            }
        }


        public void AddHttpRateLimiter(Action<RateLimiterOptions> options)
        {
            var hubconOptions = (RateLimiterOptions rlo) =>
            {
                options.Invoke(rlo);

                var previous = rlo.OnRejected;
                rlo.OnRejected = async (context, token) =>
                {
                    var converter = context.HttpContext.RequestServices.GetRequiredService<IDynamicConverter>();

                    context.HttpContext.Response.StatusCode = 429;
                    context.HttpContext.Response.ContentType = "application/json";

                    var response = converter.SerializeToElement(HubconResponse.TooManyRequests());

                    await context.HttpContext.Response.WriteAsJsonAsync(response, token);

                    if (previous != null)
                        await previous(context, token);
                };
            };

            Builder.Services.AddRateLimiter(hubconOptions);
        }
        internal readonly List<HubconTransportAttribute> DefaultTransports = new();

        public void AddTransport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : HubconTransportAttribute, new()
        {
            HubconServerBuilder.AddTransport<T>();
        }

        public void AddTransport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T attribute) where T : HubconTransportAttribute, new()
        {
            HubconServerBuilder.AddTransport<T>(attribute);
        }

        public void UseTokenValidationParameters(TokenValidationParameters tokenValidationParameters)
        {
            HubconServerBuilder.AddTokenValidationParameters(tokenValidationParameters);
        }

        public void AddConcurrencyLimiter()
        {
            HubconServerBuilder.AddGlobalMiddleware<InternalConcurrencyCheckMiddleware>((services, type) => services.TryAddSingleton(type));
        }
    }
}