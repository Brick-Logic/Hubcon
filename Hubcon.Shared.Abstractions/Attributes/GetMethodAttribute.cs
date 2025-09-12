using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class GetMethodAttribute : Attribute
    {
        public GetMethodAttribute()
        {       
        }
    }
}

