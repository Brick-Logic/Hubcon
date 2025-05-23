namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IAuthenticationManager
    {
        public abstract event Action? OnSessionIsActive;
        public abstract event Action? OnSessionIsInactive;

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