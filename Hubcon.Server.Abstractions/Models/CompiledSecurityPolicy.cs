using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Represents the resolved and immutable security configuration for a Hubcon operation.
    /// This record consolidates roles, policies, and custom handlers into a single 
    /// object to avoid redundant reflection lookups during the request lifecycle.
    /// </summary>
    /// <param name="Handlers">A collection of custom authentication and authorization handlers associated with the operation.</param>
    /// <param name="Roles">The specific security roles required to access this operation.</param>
    /// <param name="Policies">The specific named authorization policies that must be satisfied.</param>
    /// <param name="AllowAnonymous">Indicates whether the operation can be accessed without an authenticated session.</param>
    public record CompiledSecurityPolicy(
        IReadOnlyList<IUseAuthAttribute> Handlers,
        string[] Roles,
        string[] Policies,
        bool AllowAnonymous
    );
}
