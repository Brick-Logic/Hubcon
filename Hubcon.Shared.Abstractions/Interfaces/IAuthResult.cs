using System;

namespace Hubcon
{
    public interface IAuthResult
    {
        string? AccessToken { get; }
        string? TokenType { get; }
        string? ErrorMessage { get; }
        long? ExpiresAt { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; set; }
        string? RefreshToken { get; }
    }
}