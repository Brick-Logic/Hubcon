using Hubcon.Tools;
using Microsoft.AspNetCore.SignalR;
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
        
        public static IServiceCollection AddHubconControllers<TController, THub>(this IServiceCollection services, params object[] parameters) 
            where TController : ClientController
            where THub : IServerHubController
        {
            ClientController controller = InstanceCreator.TryCreateInstance<TController>(parameters).StartInstanceAsync();           
            THub connector = controller.GetConnector<THub>()!;

            services.AddSingleton(controller);
            services.AddSingleton(typeof(THub), provider => connector);
            return services;
        }

        public static IServiceCollection AddHubconControllers<TController, THub>(this IServiceCollection services, string url)
            where TController : ClientController
            where THub : IServerHubController
        {
            ClientController controller = InstanceCreator.TryCreateInstance<TController>([url]).StartInstanceAsync();
            THub connector = controller.GetConnector<THub>()!;

            services.AddSingleton(controller);
            services.AddSingleton(typeof(THub), provider => connector);
            return services;
        }

        public static IServiceCollection AddHubconClientController<TController>(this IServiceCollection services, params object[] parameters)
            where TController : ClientController
        {
            ClientController controller = InstanceCreator.TryCreateInstance<TController>(parameters).StartInstanceAsync();
            services.AddSingleton(controller);
            return services;
        }

        public static IServiceCollection AddHubconServerHub<THub>(this IServiceCollection services, Func<ClientController> implementationFactory)
            where THub : IServerHubController
        {
            ClientController controller = implementationFactory.Invoke().StartInstanceAsync();
            THub connector = controller.GetConnector<THub>()!;

            services.AddSingleton(typeof(THub), provider => connector);
            return services;
        }

        public static IServiceCollection AddHubconServerHub<TController, THub>(this IServiceCollection services, string url)
            where TController : ClientController
            where THub : IServerHubController
        {
            ClientController controller = InstanceCreator.TryCreateInstance<TController>([url]).StartInstanceAsync();
            THub connector = controller.GetConnector<THub>()!;

            services.AddSingleton(typeof(THub), provider => connector);
            return services;
        }

        public static IServiceCollection AddHubconClientAccessor(this IServiceCollection services)
        {
            services.AddScoped(typeof(IClientAccessor<,>), typeof(ClientConnectorsManager<,>));
            return services;
        }
    }
}
