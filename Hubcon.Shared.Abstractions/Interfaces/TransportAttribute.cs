using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public abstract class HubconTransport : Attribute
    {
        readonly static Dictionary<Type, HubconTransport> _defaultInstances = new Dictionary<Type, HubconTransport>();

        public static HubconTransport GetDefault<T>() where T : HubconTransport, new()
        {
            if (!_defaultInstances.TryGetValue(typeof(T), out var value))
            {
                var defaultValue = new T();
                _defaultInstances.TryAdd(typeof(T), defaultValue);
                return defaultValue;
            }

            return (T)value;
        }

        protected HubconTransport() 
        {
        }

        public abstract string TransportKey { get; }
    }
}