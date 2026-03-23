using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a contract for a persisted security session, storing the necessary 
    /// tokens and metadata required to maintain an authenticated state.
    /// </summary>
    public interface IPersistedSession
    {
        /// <summary>
        /// Gets or sets the security token used to authenticate requests.
        /// </summary>
        string? AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the expiration timestamp of the current access token.
        /// Usually represented as a Unix timestamp (seconds) or ticks.
        /// </summary>
        long? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the token used to obtain a new access token once the current one expires.
        /// </summary>
        string? RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the type of the token (e.g., "Bearer").
        /// </summary>
        string? TokenType { get; set; }
    }
}