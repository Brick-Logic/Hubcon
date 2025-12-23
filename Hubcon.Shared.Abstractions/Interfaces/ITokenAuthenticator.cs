using System.Security.Claims;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface ITokenAuthenticator
    {
        ClaimsPrincipal? Authenticate(string token);
    }
}
