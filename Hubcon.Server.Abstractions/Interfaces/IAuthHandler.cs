using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Defines the contract for a security handler responsible for authenticating requests 
    /// based on transport-specific attributes and the current operation context.
    /// </summary>
    public interface IAuthHandler
    {
        /// <summary>
        /// Asynchronously performs authentication for the current operation.
        /// </summary>
        /// <param name="context">The <see cref="IOperationContext"/> representing the current request and execution state.</param>
        /// <param name="originAttribute">The <see cref="IUseAuthAttribute"/> that triggered the authentication requirement.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> containing the <see cref="ClaimsPrincipal"/> if authentication is successful; 
        /// otherwise, <see langword="null"/>.
        /// </returns>
        ValueTask<ClaimsPrincipal?> AuthenticateAsync(IOperationContext context, IUseAuthAttribute originAttribute);
    }
}
