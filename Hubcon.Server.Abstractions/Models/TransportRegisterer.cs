using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;

namespace Hubcon
{
    public abstract class TransportRegisterer<THubconTransportAttribute, TSettings>
        where THubconTransportAttribute : HubconTransportAttribute<TSettings>, new()
        where TSettings : class, ITransportSettings, new()
    {
        private THubconTransportAttribute? _transport;
        public THubconTransportAttribute TransportAttribute
        {
            get => _transport ??= (THubconTransportAttribute)HubconTransportAttribute.GetDefault<THubconTransportAttribute>();
            private set => _transport = value;
        }
        
        private TSettings? _settings;
        public TSettings Settings
        {
            get => _settings ??= (TSettings)TransportAttribute.DefaultTransportSettings;
            private set => _settings = value;
        }
        
        public void UseTransportAttribute(THubconTransportAttribute transportAttribute)
        {
            TransportAttribute = transportAttribute;
        }
        
        public void UseSettings(TSettings settings)
        {
            Settings = settings;
        }
        
        /// <summary>
        /// Setups the transport layer before registering operations.
        /// </summary>
        public abstract void Setup(WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract void RegisterCallOperation(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract void RegisterInvokeOperation(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract void RegisterStreamOperation(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract void RegisterIngest(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>
        /// Setups the transport layer after registering operations.
        /// </summary>
        public abstract void PostRegister(WebApplication app);
    }
}