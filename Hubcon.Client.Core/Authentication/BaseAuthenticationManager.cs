using Hubcon.Client.Abstractions.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Base class for authentication managers in Hubcon client applications.
    /// Designed to be extended for custom authentication logic.
    /// </summary>
    public abstract class BaseAuthenticationManager : IAuthenticationManager
    {
        /// <summary>
        /// Raised when a session becomes active.
        /// </summary>
        public event Action? OnSessionIsActive;

        /// <summary>
        /// Raised when a session becomes inactive or is closed.
        /// </summary>
        public event Action? OnSessionIsInactive;

        /// <summary>
        /// Raised after a token is successfully refreshed.
        /// </summary>
        public event Action<IAuthResult>? OnTokenRefreshed;

        /// <summary>
        /// The type of the token in use (e.g., "Bearer").
        /// </summary>
        public string? TokenType { get; private set; }

        /// <summary>
        /// The current access token.
        /// </summary>
        public string? AccessToken { get; private set; }

        /// <summary>
        /// The current refresh token, if present.
        /// </summary>
        public string? RefreshToken { get; private set; }

        /// <summary>
        /// The token expiration time as a Unix timestamp (UTC seconds).
        /// </summary>
        public long? ExpiresAt { get; private set; }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Returns true if a session exists and the access token is not expired.
        /// </summary>
        public bool IsSessionActive
        {
            get
            {
                if (string.IsNullOrEmpty(AccessToken) || !ExpiresAt.HasValue)
                    return false;

                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                return currentTime < ExpiresAt.Value;
            }
        }

        /// <summary>
        /// Returns true if the session is nearing expiration and should be refreshed. It uses a 1 minute margin before expiration.
        /// </summary>
        public bool ShouldRefreshSession
        {
            get
            {
                if (string.IsNullOrEmpty(AccessToken) || !ExpiresAt.HasValue)
                    return false;

                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long refreshThreshold = ExpiresAt.Value - 60;

                return currentTime > refreshThreshold;
            }
        }

        /// <summary>
        /// Username associated with the current session.
        /// </summary>
        public string Username { get; protected set; } = string.Empty;

        /// <summary>
        /// Authenticates a user with a username and password.
        /// </summary>
        /// <param name="username">User’s username.</param>
        /// <param name="password">User’s password.</param>
        /// <returns>Result indicating operation outcome.</returns>
        /// <remarks>
        /// The method is thread-safe using <see cref="_semaphore"/> so only one
        /// session operation runs at a time. If the session is already active, it returns immediate success.
        /// </remarks>
        public async Task<IHubconResult> LoginAsync(string username, string password)
        {
            try
            {
                await _semaphore.WaitAsync();

                if (IsSessionActive)
                    return Result.Success();

                Username = username;

                var auth = await AuthenticateAsync(username, password);

                if (auth.IsFailure)
                {
                    OnSessionIsInactive?.Invoke();
                    return Result.Failure(auth.ErrorMessage);
                }

                TokenType = auth.TokenType;
                AccessToken = auth.AccessToken;
                RefreshToken = auth.RefreshToken;
                ExpiresAt = auth.ExpiresAt;

                await SaveSessionAsync();
                OnSessionIsActive?.Invoke();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Authenticates using an external or pre-issued token.
        /// </summary>
        /// <param name="token">Token string.</param>
        /// <param name="type">Type of the token.</param>
        /// <returns>Result indicating operation outcome.</returns>
        /// <remarks>
        /// If there is already an active session, it returns success. Persists the session after authenticating.
        /// </remarks>
        public async Task<IHubconResult> LoginWithTokenAsync(string token, string type)
        {
            try
            {
                await _semaphore.WaitAsync();

                if (IsSessionActive)
                    return Result.Success();

                var auth = await AuthenticateWithTokenAsync(token, type);

                if (auth.IsFailure)
                {
                    OnSessionIsInactive?.Invoke();
                    return Result.Failure(auth.ErrorMessage);
                }

                AccessToken = auth.AccessToken;
                RefreshToken = auth.RefreshToken;
                ExpiresAt = auth.ExpiresAt;

                await SaveSessionAsync();
                OnSessionIsActive?.Invoke();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Refreshes the current session using the stored refresh token.
        /// </summary>
        /// <returns>Result indicating whether the refresh succeeded.</returns>
        /// <remarks>
        /// This method is thread-safe and emits <see cref="OnTokenRefreshed"/> when the refresh succeeds.
        /// </remarks>
        public async Task<IHubconResult> TryRefreshSessionAsync()
        {
            try
            {
                await _semaphore.WaitAsync();

                var refresh = await RefreshSessionAsync(RefreshToken!);

                if (refresh.IsFailure)
                {
                    await LogoutAsync();
                    OnSessionIsInactive?.Invoke();
                    return Result.Failure(refresh.ErrorMessage);
                }

                TokenType = refresh.TokenType;
                AccessToken = refresh.AccessToken;
                RefreshToken = refresh.RefreshToken;
                ExpiresAt = refresh.ExpiresAt;
                OnTokenRefreshed?.Invoke(refresh);

                await SaveSessionAsync();
                OnSessionIsActive?.Invoke();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Closes the current session and clears all stored tokens.
        /// </summary>
        /// <returns>Task that completes when session is cleared.</returns>
        public async Task LogoutAsync()
        {
            try
            {
                AccessToken = null;
                RefreshToken = null;
                TokenType = null;
                ExpiresAt = null;
                await ClearSessionAsync();
                OnSessionIsInactive?.Invoke();
            }
            finally
            {
            }
        }

        /// <summary>
        /// Loads a previously saved session from persistent storage.
        /// </summary>
        /// <returns>Result indicating if a session was restored.</returns>
        /// <remarks>
        /// If the in-memory session is already valid (according to <see cref="IsSessionActive"/>), returns success without reloading.
        /// </remarks>
        public async Task<IHubconResult> LoadSessionAsync()
        {
            try
            {
                await _semaphore.WaitAsync();

                if (IsSessionActive)
                    return Result.Success();

                var session = await LoadPersistedSessionAsync();
                if (session is not null)
                {
                    AccessToken = session.AccessToken;
                    RefreshToken = session.RefreshToken;
                    ExpiresAt = session.ExpiresAt;
                    OnSessionIsActive?.Invoke();
                    return Result.Success();
                }

                OnSessionIsInactive?.Invoke();

                return Result.Failure();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Saves the current session state to persistent storage.
        /// </summary>
        /// <returns>Task that completes when state is stored.</returns>
        private async Task SaveSessionAsync()
        {
            try
            {
                var session = new PersistedSession()
                {
                    TokenType = TokenType!,
                    AccessToken = AccessToken!,
                    RefreshToken = RefreshToken,
                    ExpiresAt = ExpiresAt.HasValue ? ExpiresAt.Value : 0
                };

                await SaveSessionAsync(session);
            }
            finally
            {
            }
        }

        /// <summary>
        /// Authenticates using a username and password.
        /// </summary>
        /// <param name="username">User’s username.</param>
        /// <param name="password">User’s password.</param>
        /// <returns>The authentication result.</returns>
        protected abstract Task<IAuthResult> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Authenticates using a supplied token.
        /// </summary>
        /// <param name="token">Access token string.</param>
        /// <param name="type">Type of the token.</param>
        /// <returns>The authentication result.</returns>
        protected abstract Task<IAuthResult> AuthenticateWithTokenAsync(string token, string type);

        /// <summary>
        /// Performs a session refresh using a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token to use.</param>
        /// <returns>The authentication result.</returns>
        protected abstract Task<IAuthResult> RefreshSessionAsync(string refreshToken);

        /// <summary>
        /// Saves a session to local storage.
        /// </summary>
        /// <param name="session">Session data to store.</param>
        /// <returns>Task that completes when storage is finished.</returns>
        protected abstract Task SaveSessionAsync(PersistedSession session);

        /// <summary>
        /// Removes any persisted session from storage.
        /// </summary>
        /// <returns>Task that completes when storage is cleared.</returns>
        protected abstract Task ClearSessionAsync();

        /// <summary>
        /// Loads a session from persistent storage.
        /// </summary>
        /// <returns>The persisted session, or null if none exists.</returns>
        protected abstract Task<PersistedSession?> LoadPersistedSessionAsync();
    }

    /// <summary>
    /// Indicates the result of an operation in Hubcon.
    /// </summary>
    public class Result : IHubconResult
    {
        /// <summary>
        /// True if the operation succeeded.
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// The error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// True if the operation failed.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Creates a success result instance.
        /// </summary>
        public static IHubconResult Success() => new Result { IsSuccess = true };

        /// <summary>
        /// Creates a failed result instance.
        /// </summary>
        /// <param name="message">Error message describing the failure.</param>
        public static IHubconResult Failure(string? message = null) => new Result { IsSuccess = false, ErrorMessage = message ?? "" };
    }

    /// <summary>
    /// Represents the outcome of an authentication operation.
    /// </summary>
    public class AuthResult : IAuthResult
    {
        /// <summary>
        /// True if authentication was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// True if authentication failed.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// The access token if authentication succeeded.
        /// </summary>
        public string? AccessToken { get; private set; }

        /// <summary>
        /// The token type, such as "Bearer".
        /// </summary>
        public string? TokenType { get; private set; }

        /// <summary>
        /// The refresh token used to renew sessions.
        /// </summary>
        public string? RefreshToken { get; private set; }

        /// <summary>
        /// The expiration time as a Unix timestamp (UTC seconds).
        /// </summary>
        public long? ExpiresAt { get; private set; }

        /// <summary>
        /// An error message describing the cause of authentication failure.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Returns a success authentication result.
        /// </summary>
        /// <param name="accessToken">Access token for authenticated requests.</param>
        /// <param name="tokenType">The type of the token.</param>
        /// <param name="refreshToken">The refresh token to use for renewal.</param>
        /// <param name="expiresAtUnixSeconds">Expiration timestamp (Unix UTC seconds).</param>
        public static IAuthResult Success(string accessToken, string tokenType, string refreshToken, long? expiresAtUnixSeconds) =>
            new AuthResult() { IsSuccess = true, AccessToken = accessToken, TokenType = tokenType, RefreshToken = refreshToken, ExpiresAt = expiresAtUnixSeconds };

        /// <summary>
        /// Returns a failed authentication result.
        /// </summary>
        /// <param name="errorMessage">A description of the failure.</param>
        public static IAuthResult Failure(string? errorMessage) =>
            new AuthResult() { IsSuccess = false, ErrorMessage = errorMessage ?? "" };
    }

    /// <summary>
    /// Describes a session that can be stored and retrieved from local storage.
    /// </summary>
    public class PersistedSession : IPersistedSession
    {
        /// <summary>
        /// The access token for the session.
        /// </summary>
        public string? AccessToken { get; set; } = default!;

        /// <summary>
        /// The token type (e.g., "Bearer").
        /// </summary>
        public string? TokenType { get; set; } = default!;

        /// <summary>
        /// The refresh token associated with the session, if any.
        /// </summary>
        public string? RefreshToken { get; set; } = default!;

        /// <summary>
        /// Persisted expiration time in Unix UTC seconds. May be <c>null</c> if not set.
        /// </summary>
        public long? ExpiresAt { get; set; }
    }
}
