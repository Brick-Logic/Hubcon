using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Attributes
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconControllerAttribute : Attribute
    {
        public HubconControllerAttribute()
        {
            
        }
    }
}
