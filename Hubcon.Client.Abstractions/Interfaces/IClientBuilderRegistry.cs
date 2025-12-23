using Microsoft.Extensions.DependencyInjection;
using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientBuilderRegistry
    {
        bool GetClientBuilder(Type contractType, out IClientBuilder? value);
        void RegisterModule<TRemoteServerModule>(IServiceCollection services, Func<TRemoteServerModule>? remoteServerFactory = null) where TRemoteServerModule : class, IRemoteServerModule;
    }
}