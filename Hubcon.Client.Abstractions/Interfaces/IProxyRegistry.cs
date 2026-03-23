#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IProxyRegistry
    {
        void RegisterProxy(Type interfaceType, Type proxyType);
        Type TryGetProxy<T>() where T : IControllerContract;
        Type TryGetProxy(Type interfaceType);
        void TryRegisterProxyByContract(Type contractType);
    }
}
