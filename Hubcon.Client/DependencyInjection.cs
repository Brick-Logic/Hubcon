using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddHubconClient(this IServiceCollection services)
        {
            HubconClientBuilder.Current.AddHubconClient(services);
            return services;
        }

        public static IServiceCollection AddRemoteServerModule<TRemoteServerModule>(this IServiceCollection services)
             where TRemoteServerModule : IRemoteServerModule, new()
        {
            HubconClientBuilder.Current.AddRemoteServerModule<TRemoteServerModule>(services);
            return services;
        }

        public static IServiceCollection UseContractsFromAssembly(this IServiceCollection services, string assemblyName)
        {
            HubconClientBuilder.Current.UseContractsFromAssembly(services, assemblyName);
            return services;
        }
    }
}