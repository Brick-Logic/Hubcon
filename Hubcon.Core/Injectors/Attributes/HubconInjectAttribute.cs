using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Injectors.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class HubconInjectAttribute : Attribute
    {
        public Type? Type { get; }

        public HubconInjectAttribute(Type? type = null)
        {
            Type = type;
        }
    }
}
