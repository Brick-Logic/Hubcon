using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UseHttpRateLimiterAttribute(string Policy) : Attribute
    {
        public string Policy { get; } = Policy;
    }
}