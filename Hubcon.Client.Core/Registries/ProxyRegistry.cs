using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Linq;

namespace Hubcon.Client.Core.Registries
{
    public class ProxyRegistry : IProxyRegistry
    {
        private Dictionary<Type, Type> ProxyTypes { get; } = new();

        public void RegisterProxy(Type interfaceType, Type proxyType)
        {
            if (proxyType == null || interfaceType == null)
                throw new ArgumentNullException(nameof(proxyType), "El tipo de interfaz o el tipo de proxy no pueden ser nulos.");

            if (ProxyTypes.ContainsKey(interfaceType))
                throw new ArgumentException($"El proxy para {interfaceType} ya está registrado.");

            ProxyTypes[interfaceType] = proxyType;
        }

        public void TryRegisterProxyByContract<T>() where T : IControllerContract
        {
            TryRegisterProxyByContract(typeof(T));
        }

        public void TryRegisterProxyByContract(Type contractType)
        {
            if (contractType.IsAssignableTo(typeof(IControllerContract)))
                throw new ArgumentException($"El tipo {contractType.Name} no implementa {nameof(IControllerContract)}");

            var assembly = contractType.Assembly;

            var implementation = assembly
                .GetTypes()
                .ToList()
                .Find(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.Name == contractType.Name + "Proxy");

            if(implementation == null)
                throw new ArgumentNullException($"No se detectó una implementación para el contrato {contractType.Name}.");

            RegisterProxy(contractType, implementation);
        }

        public Type TryGetProxy<T>() where T : IControllerContract
        {
            return TryGetProxy(typeof(T))!;
        }
        
        public Type TryGetProxy(Type interfaceType)
        {
            if (interfaceType.IsAssignableFrom(typeof(IControllerContract)))
                throw new ArgumentException($"El tipo '{interfaceType.Name}' no implementa {nameof(IControllerContract)}.");

            if (ProxyTypes.TryGetValue(interfaceType, out Type? proxy))
                return proxy;

            var assembly = interfaceType.Assembly;

            var implementation = assembly
                .GetTypes()
                .ToList()
                .Find(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.Name == interfaceType.Name + "Proxy");

            RegisterProxy(interfaceType, implementation!);

            if (implementation != null) return implementation;

            return default!;
        }
    }
}
