using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Hubcon transport layer attribute. Use this to implement custom transport implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public abstract class HubconTransportAttribute : Attribute
    {
        readonly static Dictionary<Type, HubconTransportAttribute> _defaultInstances = new();

        /// <summary>
        /// Gets a default implementation for a Hubcon transport attribute and caches the result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
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


        /// <summary>
        /// The transport key used to identify the transport internally.
        /// </summary>
        public abstract string TransportKey { get; }
    }
}