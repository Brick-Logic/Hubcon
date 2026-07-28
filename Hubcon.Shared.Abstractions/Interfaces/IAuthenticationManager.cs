#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IBuildableAuthenticationManager
    {
        void Build(TimeSpan margin, TimeSpan interval);
    }
}

namespace Hubcon
{
    /// <summary>
    /// Provides an abstraction for managing authentication state and session tokens within the application.
    /// </summary>
    /// <remarks>
    /// This interface defines the contract for handling authentication, including session management, token handling, and event notification for authentication state changes.
    /// </remarks>
    public interface IAuthenticationManager : IBuildableAuthenticationManager
    {
        /// <summary>
        /// Occurs when a valid session has been loaded or started and the application should treat the session as active.
        /// </summary>
        public event Action? OnSessionIsActive;

        /// <summary>
        /// Occurs when the session is no longer valid or the user signs out.
        /// </summary>
        public event Action? OnSessionIsInactive;

        /// <summary>
        /// Occurs when the access token has been successfully refreshed.
        /// </summary>
        public event Action<IAuthResult>? OnTokenRefreshed;

        /// <summary>
        /// Gets the current access token used for authenticating requests.
        /// </summary>
        /// <value>
        /// The access token, or <c>null</c> if there is no active session or the session has not been loaded.
        /// </value>
        string? AccessToken { get; }

        /// <summary>
        /// Gets the expiration timestamp (in seconds since epoch) of the current access token.
        /// </summary>
        /// <value>
        /// The expiration timestamp, or <c>null</c> if not available or not applicable.
        /// </value>
        long? ExpiresAt { get; }

        /// <summary>
        /// Gets a value indicating whether there is currently a valid session.
        /// </summary>
        /// <value>
        /// <c>true</c> if a valid session is active; otherwise, <c>false</c>.
        /// </value>
        bool IsSessionActive { get; }

        /// <summary>
        /// Gets the refresh token used to obtain new access tokens without requiring the user to re-authenticate.
        /// </summary>
        /// <value>
        /// The refresh token, or <c>null</c> if not available or not applicable.
        /// </value>
        string? RefreshToken { get; }

        /// <summary>
        /// Gets the token type (for example, "Bearer").
        /// </summary>
        /// <value>
        /// The token type, or <c>null</c> if not set.
        /// </value>
        string? TokenType { get; }

        /// <summary>
        /// Gets a value indicating whether the session should be refreshed based on the implementation's logic.
        /// </summary>
        /// <value>
        /// <c>true</c> if the session should be refreshed; otherwise, <c>false</c>.
        /// </value>
        bool ShouldRefreshSession { get; }

        /// <summary>
        /// Loads the persisted session state, if any, and updates the corresponding properties.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the operation result.
        /// </returns>
        Task<IHubconResult> LoadSessionAsync();

        /// <summary>
        /// Attempts to authenticate with the provided credentials and establish a new session.
        /// </summary>
        /// <param name="username">The username or user identifier.</param>
        /// <param name="password">The user password.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result indicates whether the login was successful.
        /// </returns>
        Task<IHubconResult> LoginAsync(string username, string password);

        /// <summary>
        /// Signs out the current session, clears tokens, and notifies subscribers.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        Task LogoutAsync();

        /// <summary>
        /// Attempts to refresh the session using the available refresh token.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the result of the refresh attempt.
        /// </returns>
        Task<IHubconResult> TryRefreshSessionAsync();

        /// <summary>
        /// Starts a session using a token already obtained by another mechanism.
        /// </summary>
        /// <param name="token">The token to use as the access token.</param>
        /// <param name="type">The token type (for example, "Bearer").</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result indicates whether the operation was accepted.
        /// </returns>
        Task<IHubconResult> LoginWithTokenAsync(string token, string type);
    }
}