namespace Hubcon.Shared.Abstractions.Interfaces
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