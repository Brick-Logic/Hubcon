using System;
using System.ComponentModel;

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
