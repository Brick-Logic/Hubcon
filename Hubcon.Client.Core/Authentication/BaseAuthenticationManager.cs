using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Client.Core.Authentication
{
    public abstract class BaseAuthenticationManager : IAuthenticationManager
    {
        public event Action? OnSessionIsActive;
        public event Action? OnSessionIsInactive;

        public abstract string? AccessToken { get; protected set; }
        public abstract string? RefreshToken { get; protected set; }
        public abstract DateTime? AccessTokenExpiresAt { get; protected set; }

        public bool IsSessionActive => !string.IsNullOrEmpty(AccessToken);

        public string Username { get; protected set; } = string.Empty;
        public string Password { get; protected set; } = string.Empty;

        public async Task<IResult> LoginAsync(string username, string password)
        {
            Username = username;
            Password = password;

            var auth = await AuthenticateAsync(username, password);

            if (auth.IsFailure)
            {
                OnSessionIsInactive?.Invoke();
                return Result.Failure(auth.ErrorMessage);
            }

            AccessToken = auth.AccessToken;
            RefreshToken = auth.RefreshToken;
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(auth.ExpiresInSeconds);

            await SaveSessionAsync();
            OnSessionIsActive?.Invoke();


            return Result.Success();
        }

        public async Task<IResult> TryRefreshSessionAsync()
        {
            //if (string.IsNullOrEmpty(RefreshToken))
            //    return Result.Failure("No refresh token available.");

            var refresh = await RefreshSessionAsync(RefreshToken!);

            if (refresh.IsFailure)
            {
                await ClearSessionAsync();
                OnSessionIsInactive?.Invoke();
                return Result.Failure("Refresh failed");
            }

            AccessToken = refresh.AccessToken;
            RefreshToken = refresh.RefreshToken;
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(refresh.ExpiresInSeconds);

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

        public async Task<IResult> LoadSessionAsync()
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

        protected abstract Task<IAuthResult> AuthenticateAsync(string username, string password);
        protected abstract Task<IAuthResult> RefreshSessionAsync(string refreshToken);
        protected abstract Task SaveSessionAsync();
        protected abstract Task ClearSessionAsync();
        protected abstract Task<PersistedSession?> LoadPersistedSessionAsync();
    }

    public class Result : IResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }
        public bool IsFailure => !IsSuccess;

        public static IResult Success() => new Result { IsSuccess = true };
        public static IResult Failure(string? message = null) => new Result { IsSuccess = false, ErrorMessage = message ?? "" };
    }

    public class AuthResult : IAuthResult
    {
        public bool IsSuccess { get; set; }
        public bool IsFailure => !IsSuccess;
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public int ExpiresInSeconds { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static IAuthResult Success(string accessToken, string refreshToken, int expiresInSeconds) =>
            new AuthResult() { IsSuccess = true, AccessToken = accessToken, RefreshToken = refreshToken, ExpiresInSeconds = expiresInSeconds };

        public static IAuthResult Failure(string? errorMessage) =>
            new AuthResult() { IsSuccess = false, ErrorMessage = errorMessage ?? "" };
    }

    public class PersistedSession : IPersistedSession
    {
        public string AccessToken { get; set; } = default!;
        public string? RefreshToken { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
    }
}
