using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Converters;
using Hubcon.Core.Handlers;
using Hubcon.Core.Interceptors;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Middleware;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Models.Pipeline.Interfaces;
using Hubcon.Core.Registries;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Core
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddHubcon(this IServiceCollection services, Action<IServiceCollection>? additionalServices = null)
        {
            services.AddSingleton<DynamicConverter>();
            services.AddSingleton<StreamNotificationHandler>();
            services.AddScoped(typeof(ClientControllerConnectorInterceptor<>));
            services.AddSingleton<ClientRegistry>();
            services.AddScoped<MethodInvokerProvider>();
            services.AddScoped<IMiddlewareProvider, MiddlewareProvider>();
            services.AddScoped<RequestPipeline>();

            additionalServices?.Invoke(services);

            services.AddScoped(typeof(HubconControllerManager<>));

            return services;
        }

        public static IServiceCollection AddHubconClient(this IServiceCollection services, Action<IServiceCollection>? additionalServices = null)
        {
            services.AddSingleton<DynamicConverter>();
            services.AddScoped<MethodInvokerProvider>();
            services.AddScoped<IMiddlewareProvider, MiddlewareProvider>();
            services.AddScoped<RequestPipeline>();

            additionalServices?.Invoke(services);

            services.AddScoped(typeof(HubconControllerManager<>));

            return services;
        }


        public static WebApplication? UseHubcon(this WebApplication e)
        {
            StaticServiceProvider.Setup(e);
            return e;
        }

        /// <summary>
        /// Agrega un servicio IClientAccessor como servicio scoped que permite acceder a los clientes de un hub usando la interfaz IClientAccessor<THub, TIClientController>.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddHubconClientAccessor(this IServiceCollection services)
        {
            services.AddScoped(typeof(IClientAccessor<,>), typeof(HubconClientConnector<,>));
            return services;
        }

        public static IServiceCollection AddHubconController<T>(this IServiceCollection services, Action<IPipelineOptions>? options = null)
            where T : class, IBaseHubconController, ICommunicationContract
        {
            var controllerType = typeof(T);
            List<Type> implementationTypes = controllerType
                .GetInterfaces()
                .Where(x => typeof(ICommunicationContract).IsAssignableFrom(x))
                .ToList();

            if (implementationTypes.Count == 0)
                throw new InvalidOperationException($"Controller {controllerType.Name} does not implement ICommunicationContract.");

            
            services.AddScoped<T>();

            if(options != null)
            {
                MiddlewareProvider.AddMiddlewares<T>(options, services);
                services.AddScoped<T>();
            }

            return services;
        }
    }
}
