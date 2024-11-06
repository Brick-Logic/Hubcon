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
    }
}
