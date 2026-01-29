using System;

namespace Hubcon
{
    public interface IAuthResult
    {
        string? AccessToken { get; }
        string? TokenType { get; }
        string? ErrorMessage { get; }
        DateTime ExpiresInSeconds { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; set; }
        string? RefreshToken { get; }
    }
}