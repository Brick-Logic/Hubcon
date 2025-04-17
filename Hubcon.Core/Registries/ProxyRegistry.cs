using Castle.Core.Internal;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Hubcon.Core.Registries
{
    public class ProxyRegistry
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

        public void TryRegisterProxyByContract<T>() where T : ICommunicationContract
            => TryRegisterProxyByContract(typeof(T));
        public void TryRegisterProxyByContract(Type contractType)
        {
            if (contractType.IsAssignableTo(typeof(ICommunicationContract)))
                throw new ArgumentException($"El tipo {contractType.Name} no implementa {nameof(ICommunicationContract)}");

            var assembly = contractType.Assembly;

            var implementation = assembly
                .GetTypes()
                .Find(t => !t.IsInterface && typeof(ICommunicationContract).IsAssignableFrom(t) && t.Name == contractType.Name + "Proxy");

            if(implementation == null)
                throw new ArgumentNullException($"No se detectó una implementación para el contrato {contractType.Name}.");

            RegisterProxy(contractType, implementation);
        }

        public Type TryGetProxy<T>() where T : ICommunicationContract
        {
            if (ProxyTypes.TryGetValue(typeof(T), out Type? proxy))
                return proxy;

            var assembly = typeof(T).Assembly;

            var implementation = assembly
                .GetTypes()
                .Find(t => !t.IsInterface && typeof(ICommunicationContract).IsAssignableFrom(t) && t.Name == typeof(T).Name + "Proxy");

            RegisterProxy(typeof(T), implementation);

            if (implementation != null) return implementation;

            return default!;
        }
        
        public Type? TryGetProxy(Type interfaceType)
        {

            if (ProxyTypes.TryGetValue(interfaceType, out Type? proxy))
                return proxy;
            
            return default;
        }
    }
}
