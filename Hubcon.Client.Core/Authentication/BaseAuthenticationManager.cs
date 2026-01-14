using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Client.Core.Authentication
{
    public abstract class BaseAuthenticationManager : IAuthenticationManager
    {
        public event Action? OnSessionIsActive;
        public event Action? OnSessionIsInactive;

        public string? TokenType { get; private set; }
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public DateTime? AccessTokenExpiresAt { get; private set; }

        public bool IsSessionActive => !string.IsNullOrEmpty(AccessToken);

        public string Username { get; protected set; } = string.Empty;

        public async Task<IHubconResult> LoginAsync(string username, string password)
        {
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
            AccessTokenExpiresAt = auth.ExpiresInSeconds;

            await SaveSessionAsync();
            OnSessionIsActive?.Invoke();

            return Result.Success();
        }

        public async Task<IHubconResult> LoginWithTokenAsync(string token, string type)
        {
            var auth = await AuthenticateWithTokenAsync(token, type);

            if (auth.IsFailure)
            {
                OnSessionIsInactive?.Invoke();
                return Result.Failure(auth.ErrorMessage);
            }

            AccessToken = auth.AccessToken;
            RefreshToken = auth.RefreshToken;
            AccessTokenExpiresAt = auth.ExpiresInSeconds;

            await SaveSessionAsync();
            OnSessionIsActive?.Invoke();

            return Result.Success();
        }

        public async Task<IHubconResult> TryRefreshSessionAsync()
        {
            var refresh = await RefreshSessionAsync(RefreshToken!);

            if (refresh.IsFailure)
            {
                await ClearSessionAsync();
                OnSessionIsInactive?.Invoke();
                return Result.Failure("Refresh failed");
            }

            TokenType = refresh.TokenType;
            AccessToken = refresh.AccessToken;
            RefreshToken = refresh.RefreshToken;
            AccessTokenExpiresAt = refresh.ExpiresInSeconds;


            await SaveSessionAsync();
            OnSessionIsActive?.Invoke();

            return Result.Success();
        }

        public async Task LogoutAsync()
        {
            AccessToken = null;
            RefreshToken = null;
            AccessTokenExpiresAt = null;
            await ClearSessionAsync();
            OnSessionIsInactive?.Invoke();
        }

        public async Task<IHubconResult> LoadSessionAsync()
        {
            var session = await LoadPersistedSessionAsync();
            if (session is not null)
            {
                AccessToken = session.AccessToken;
                RefreshToken = session.RefreshToken;
                AccessTokenExpiresAt = session.ExpiresAt;
                OnSessionIsActive?.Invoke();
                return Result.Success();
            }

            OnSessionIsInactive?.Invoke();

            return Result.Failure();
        }

        private async Task SaveSessionAsync()
        {
            var session = new PersistedSession()
            {
                TokenType = TokenType!,
                AccessToken = AccessToken!,
                RefreshToken = RefreshToken,
                ExpiresAt = AccessTokenExpiresAt.HasValue ? AccessTokenExpiresAt.Value : DateTime.MinValue
            };

            await SaveSessionAsync(session);
        }

        protected abstract Task<IAuthResult> AuthenticateAsync(string username, string password);
        protected abstract Task<IAuthResult> AuthenticateWithTokenAsync(string token, string type);
        protected abstract Task<IAuthResult> RefreshSessionAsync(string refreshToken);
        protected abstract Task SaveSessionAsync(PersistedSession session);
        protected abstract Task ClearSessionAsync();
        protected abstract Task<PersistedSession?> LoadPersistedSessionAsync();
    }

    public class Result : IHubconResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }
        public bool IsFailure => !IsSuccess;

        public static IHubconResult Success() => new Result { IsSuccess = true };
        public static IHubconResult Failure(string? message = null) => new Result { IsSuccess = false, ErrorMessage = message ?? "" };
    }

    public class AuthResult : IAuthResult
    {
        public bool IsSuccess { get; set; }
        public bool IsFailure => !IsSuccess;
        public string? AccessToken { get; private set; }
        public string? TokenType { get; private set; }
        public string? RefreshToken { get; private set; }
        public DateTime ExpiresInSeconds { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static IAuthResult Success(string accessToken, string tokenType, string refreshToken, DateTime expiresInSeconds) =>
            new AuthResult() { IsSuccess = true, AccessToken = accessToken, TokenType = tokenType, RefreshToken = refreshToken, ExpiresInSeconds = expiresInSeconds };

        public static IAuthResult Failure(string? errorMessage) =>
            new AuthResult() { IsSuccess = false, ErrorMessage = errorMessage ?? "" };
    }

    public class PersistedSession : IPersistedSession
    {
        public string AccessToken { get; set; } = default!;
        public string TokenType { get; set; } = default!;
        public string? RefreshToken { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
    }
}
