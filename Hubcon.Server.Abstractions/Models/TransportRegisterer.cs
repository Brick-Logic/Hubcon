using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;

namespace Hubcon
{
    public abstract class TransportRegisterer
    {
        /// <summary>
        /// Setups the transport layer before registering operations.
        /// </summary>
        public abstract bool Setup(WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract bool RegisterCallOperation(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract bool RegisterInvokeOperation(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract bool RegisterStreamOperation(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>k
        /// Registers call operations.
        /// </summary>
        public abstract bool RegisterIngest(IOperationBlueprint blueprint, WebApplication app);
        
        /// <summary>
        /// Setups the transport layer after registering operations.
        /// </summary>
        public abstract bool PostRegister(WebApplication app);
    }
}