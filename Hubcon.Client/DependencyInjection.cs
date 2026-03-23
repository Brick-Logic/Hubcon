#pragma warning disable CS1591
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Hubcon
{
    public static class DependencyInjection
    {
        /// <summary>
        /// Adds Hubcon's services to the service collection.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddHubconClient(this IServiceCollection services)
        {
            HubconClientBuilder.Current.AddHubconClient(services);
            return services;
        }

        /// <summary>
        /// Registers a remote server module to Hubcon.
        /// </summary>
        /// <typeparam name="TRemoteServerModule"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddRemoteServerModule<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TRemoteServerModule>(this IServiceCollection services)
             where TRemoteServerModule : class, IRemoteServerModule, new()
        {
            HubconClientBuilder.Current.AddRemoteServerModule<TRemoteServerModule>(services, null);
            return services;
        }

        /// <summary>
        /// Registers a remote server module to Hubcon.
        /// </summary>
        /// <typeparam name="TRemoteServerModule"></typeparam>
        /// <param name="services"></param>
        /// <param name="remoteServerFactory"></param>
        /// <returns></returns>
        public static IServiceCollection AddRemoteServerModule<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TRemoteServerModule>(this IServiceCollection services, Func<TRemoteServerModule> remoteServerFactory)
             where TRemoteServerModule : class, IRemoteServerModule
        {
            HubconClientBuilder.Current.AddRemoteServerModule<TRemoteServerModule>(services, remoteServerFactory);
            return services;
        }

        //public static IServiceCollection UseContractsFromAssembly(this IServiceCollection services, string assemblyName)
        //{
        //    HubconClientBuilder.Current.UseContractsFromAssembly(services, assemblyName);
        //    return services;
        //}
    }
}