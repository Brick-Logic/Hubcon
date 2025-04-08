using Hubcon.Core.Connectors;
using Hubcon.Core.Middleware;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Core
{
    public static class DependencyInjection
    {
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
                var middlewareProvider = new MiddlewareProvider();
                middlewareProvider.AddMiddlewares<T>(options);
            }

            return services;
        }
    }
}
