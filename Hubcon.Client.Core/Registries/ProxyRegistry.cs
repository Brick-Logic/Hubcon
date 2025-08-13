using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;

namespace Hubcon.Client.Core.Registries
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ProxyRegistry : IProxyRegistry
    {
        private readonly ConcurrentDictionary<Type, Type> _proxyTypes = new();

        public void RegisterProxy(Type interfaceType, Type proxyType)
        {
            if (interfaceType == null)
                throw new ArgumentNullException(nameof(interfaceType));
            if (proxyType == null)
                throw new ArgumentNullException(nameof(proxyType));

            if (!_proxyTypes.TryAdd(interfaceType, proxyType))
            {
                throw new ArgumentException($"El proxy para {interfaceType.Name} ya está registrado.");
            }
        }

        public void TryRegisterProxyByContract<T>() where T : IControllerContract
        {
            TryRegisterProxyByContract(typeof(T));
        }

        public void TryRegisterProxyByContract(Type contractType)
        {
            if (!typeof(IControllerContract).IsAssignableFrom(contractType))
                throw new ArgumentException($"El tipo {contractType.Name} no implementa {nameof(IControllerContract)}");

            var assembly = contractType.Assembly;

            var implementation = assembly
                .GetTypes()
                .FirstOrDefault(t => !t.IsInterface
                                     && typeof(IControllerContract).IsAssignableFrom(t)
                                     && t.Name == contractType.Name + "Proxy");

            if (implementation == null)
                throw new ArgumentNullException($"No se detectó una implementación para el contrato {contractType.Name}.");

            RegisterProxy(contractType, implementation);
        }

        public Type? TryGetProxy<T>() where T : IControllerContract
        {
            return TryGetProxy(typeof(T));
        }

        public Type? TryGetProxy(Type interfaceType)
        {
            if (!typeof(IControllerContract).IsAssignableFrom(interfaceType))
                throw new ArgumentException($"El tipo '{interfaceType.Name}' no implementa {nameof(IControllerContract)}.");

            if (_proxyTypes.TryGetValue(interfaceType, out var proxy))
                return proxy;

            var assembly = interfaceType.Assembly;

            var implementation = assembly
                .GetTypes()
                .FirstOrDefault(t => !t.IsInterface
                                     && typeof(IControllerContract).IsAssignableFrom(t)
                                     && t.Name == interfaceType.Name + "Proxy");

            if (implementation != null)
            {
                RegisterProxy(interfaceType, implementation);
                return implementation;
            }

            return null;
        }
    }

}
