namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IAuthResult
    {
        string? AccessToken { get; }
        string? ErrorMessage { get; }
        DateTime ExpiresInSeconds { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; set; }
        string? RefreshToken { get; }
    }
}