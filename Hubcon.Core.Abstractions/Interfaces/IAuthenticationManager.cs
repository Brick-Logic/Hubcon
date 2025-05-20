using Hubcon.Core.Authentication;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IAuthenticationManager
    {
        string? AccessToken { get; }
        DateTime? AccessTokenExpiresAt { get; }
        bool IsSessionActive { get; }
        string? RefreshToken { get; }

        Task<IResult> LoadSessionAsync();
        Task<IResult> LoginAsync(string username, string password);
        Task LogoutAsync();
        Task<IResult> TryRefreshSessionAsync();
    }
}