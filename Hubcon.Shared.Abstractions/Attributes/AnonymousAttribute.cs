using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Allows anonymous access to a hubcon contract or endpoint. Use in contracts to easily prevent clients from sending tokens to endpoints that do not require authentication.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class AnonymousAttribute : Attribute
    {
    }
}
