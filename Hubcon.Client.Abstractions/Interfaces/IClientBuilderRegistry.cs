using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientBuilderRegistry
    {
        bool GetClientBuilder(Type contractType, out IClientBuilder? value);
        void RegisterModule<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TRemoteServerModule>(IServiceCollection services, Func<TRemoteServerModule>? remoteServerFactory = null) where TRemoteServerModule : class, IRemoteServerModule;
    }
}