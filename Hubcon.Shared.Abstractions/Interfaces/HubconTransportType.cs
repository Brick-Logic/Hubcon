using System;
using System.Collections.Generic;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
                    AttributeTargets.Property)]
    public abstract class HubconTransportAttribute<TSettings> : HubconTransportAttribute
        where TSettings : class, ITransportSettings, new()
    {
        public HubconTransportAttribute()
        {
            _defaultTransportSettings = new TSettings();
        }
        
        /// <summary>
        /// The default settings for this transport.
        /// </summary>
        public new ITransportSettings DefaultTransportSettings => _defaultTransportSettings ??= new TSettings();
        
        /// <summary>
        /// Gets a default implementation for a Hubcon transport attribute and caches the result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static HubconTransportAttribute<TSettings> GetDefault<T>() where T : HubconTransportAttribute<TSettings>, new()
        {
            if (!_defaultInstances.TryGetValue(typeof(T), out var value))
            {
                var defaultValue = new T();
                _defaultInstances.TryAdd(typeof(T), defaultValue);
                return defaultValue;
            }

            return (T)value;
        }

        public virtual TSettings TypedDefaultTransportSettings => (TSettings)DefaultTransportSettings;
    }
    
    /// <summary>
    /// Hubcon transport layer attribute. Use this to implement custom transport implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public abstract class HubconTransportAttribute : Attribute
    {
        protected static readonly Dictionary<Type, HubconTransportAttribute> _defaultInstances = new();

        /// <summary>
        /// The transport key used to identify the transport internally.
        /// </summary>
        public abstract string TransportKey { get; }
        
        /// <summary>
        /// The ID used for high-speed telemetry.
        /// </summary>
        public abstract int TelemetryId { get; }
        
        /// <summary>
        /// Gets the registered transport counts. The transports will only be registered on the server after they are used.
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyDictionary<Type, HubconTransportAttribute> GetAllTransports()
        {
            return _defaultInstances;
        }

        /// <summary>
        /// Gets the registered transport counts. The transports will only be registered on the server after they are used.
        /// </summary>
        /// <returns></returns>
        public static int GetTransportsCount()
        {
            return _defaultInstances.Count;
        }
        
        /// <summary>
        /// Gets a default implementation for a Hubcon transport attribute and caches the result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetDefault<T>() where T : HubconTransportAttribute, new()
        {
            if (!_defaultInstances.TryGetValue(typeof(T), out var value))
            {
                var defaultValue = new T();
                _defaultInstances.TryAdd(typeof(T), defaultValue);
                return defaultValue;
            }

            return (T)value;
        }

        protected ITransportSettings? _defaultTransportSettings;

        /// <summary>
        /// The default settings for this transport.
        /// </summary>
        public virtual ITransportSettings DefaultTransportSettings => _defaultTransportSettings ??= new TransportSettings();
    }
}