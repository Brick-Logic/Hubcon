using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    /// <summary>
    /// Base attribute class for hubcon auth attributes
    /// </summary>
    /// <typeparam name="THandler"></typeparam>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public abstract class UseAuthAttribute<THandler> : Attribute, IUseAuthAttribute where THandler : IAuthHandler
    {
        /// <summary>
        /// The auth handler type
        /// </summary>
        public Type HandlerType => typeof(THandler);
    }
}
