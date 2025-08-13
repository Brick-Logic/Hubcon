using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface ITokenAuthenticator
    {
        ClaimsPrincipal? Authenticate(string token);
    }
}
