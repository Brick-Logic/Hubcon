namespace Hubcon.Core.Authentication
{
    public interface IAuthResult
    {
        string? AccessToken { get; }
        string? ErrorMessage { get; }
        int ExpiresInSeconds { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; set; }
        string? RefreshToken { get; }
    }
}