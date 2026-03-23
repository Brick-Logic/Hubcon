using System;
#pragma warning disable CS1591
using System.ComponentModel;

namespace Hubcon.Shared.Core.Attributes
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconProxyAttribute : Attribute
    {
        public HubconProxyAttribute()
        {

        }
    }
}
