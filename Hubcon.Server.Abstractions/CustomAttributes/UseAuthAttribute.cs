using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class UseAuthAttribute<THandler> : Attribute, IUseAuthAttribute where THandler : IAuthHandler
    {
        public Type HandlerType => typeof(THandler);
    }
}
