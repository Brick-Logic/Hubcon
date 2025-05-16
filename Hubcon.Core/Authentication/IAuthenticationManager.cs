
namespace Hubcon.Core.Authentication
{
    public interface IAuthenticationManager
    {
        string? AccessToken { get; }
        DateTime? AccessTokenExpiresAt { get; }
        bool IsSessionActive { get; }
        string? RefreshToken { get; }

        Task<Result> LoadSessionAsync();
        Task<Result> LoginAsync(string username, string password);
        Task LogoutAsync();
        Task<Result> TryRefreshSessionAsync();
    }
}