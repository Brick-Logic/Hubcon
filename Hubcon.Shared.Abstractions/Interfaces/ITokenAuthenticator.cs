#pragma warning disable CS1591
using System.Security.Claims;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface ITokenAuthenticator
    {
        ClaimsPrincipal? Authenticate(string token);
    }
}
