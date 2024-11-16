using Hubcon.Tools;
using Hubcon.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddHubcon(this IServiceCollection services) 
        {
            services.AddSignalR().AddMessagePackProtocol();

            return services;
        }

        /// <summary>
        /// Este metodo inicia la instancia de <typeparamref name="TController"/> que se conecta a la URL indicada y
        /// agrega un cliente singleton de tipo <typeparamref name="TServerHubInterface"/> que utiliza <typeparamref name="TController"/> para comunicarse con el servidor.
        /// </summary>
        /// <typeparam name="TController"></typeparam>
        /// <typeparam name="TServerHubInterface"></typeparam>
        /// <param name="services"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public static IServiceCollection AddHubconClientController<TController, TServerHubInterface>(this IServiceCollection services, string url, Action<string>? consoleOutput = null)
            where TController : ClientController
            where TServerHubInterface : IServerHubController
        {
            ClientController controller = InstanceCreator.TryCreateInstance<TController>([url]).StartInstanceAsync(consoleOutput);
            TServerHubInterface connector = controller.GetConnector<TServerHubInterface>()!;

            services.AddSingleton(typeof(TServerHubInterface), provider => connector);
            return services;
        }

        /// <summary>
        /// Agrega un servicio IClientAccessor como servicio scoped que permite acceder a los clientes de un hub usando la interfaz IClientAccessor<THub, TIClientController>.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddHubconClientAccessor(this IServiceCollection services)
        {
            services.AddScoped(typeof(ClientControllerConnectorInterceptor<>));
            services.AddScoped(typeof(IClientAccessor<,>), typeof(ClientConnector<,>));
            return services;
        }
    }
}
