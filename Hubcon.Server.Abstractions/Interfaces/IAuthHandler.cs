using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public interface IAuthHandler
    {
        ValueTask<ClaimsPrincipal?> AuthenticateAsync(IOperationContext context, IUseAuthAttribute originAttribute);
    }
}
