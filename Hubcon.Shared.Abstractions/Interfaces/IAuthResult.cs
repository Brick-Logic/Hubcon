using System;

namespace Hubcon
{
    /// <summary>
    /// Defines the standardized result of an authentication or token-refresh operation.
    /// Encapsulates access credentials, expiration metadata, and failure details.
    /// </summary>
    public interface IAuthResult
    {
        /// <summary>Gets the security token used to authorize subsequent requests (e.g., a JWT Bearer token).</summary>
        string? AccessToken { get; }

        /// <summary>Gets the type of the token issued, typically "Bearer".</summary>
        string? TokenType { get; }

        /// <summary>Gets a descriptive message explaining why the authentication attempt failed.</summary>
        string? ErrorMessage { get; }

        /// <summary>Gets the Unix timestamp (seconds) indicating when the <see cref="AccessToken"/> expires.</summary>
        long? ExpiresAt { get; }

        /// <summary>Gets a value indicating whether the authentication attempt failed.</summary>
        bool IsFailure { get; }

        /// <summary>Gets or sets a value indicating whether the authentication attempt was successful.</summary>
        bool IsSuccess { get; set; }

        /// <summary>Gets the token used to obtain a new <see cref="AccessToken"/> after it expires.</summary>
        string? RefreshToken { get; }
    }
}