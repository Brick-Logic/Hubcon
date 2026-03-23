using Hubcon.Server.Abstractions.CustomAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Specifies that a valid JSON Web Token (JWT) is required to access the 
    /// decorated contract or operation. This attribute triggers the 
    /// <see cref="JwtAuthHandler"/> during the request lifecycle.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseJwtAttribute : UseAuthAttribute<JwtAuthHandler>
    {
        
    }
}