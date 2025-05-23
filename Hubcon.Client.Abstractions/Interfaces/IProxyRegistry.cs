using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
