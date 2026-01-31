using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public abstract class HubconTransportAttribute : Attribute
    {
        readonly static Dictionary<Type, HubconTransportAttribute> _defaultInstances = new();

        public static HubconTransportAttribute GetDefault<T>() where T : HubconTransportAttribute, new()
        {
            if (!_defaultInstances.TryGetValue(typeof(T), out var value))
            {
                var defaultValue = new T();
                _defaultInstances.TryAdd(typeof(T), defaultValue);
                return defaultValue;
            }

            return (T)value;
        }

        protected HubconTransportAttribute() 
        {
        }

        public abstract string TransportKey { get; }
    }
}