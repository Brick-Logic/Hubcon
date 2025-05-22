using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.Core.Authentication
{
    public abstract class BaseAuthenticationManager : IAuthenticationManager
    {
        public abstract string? AccessToken { get; protected set; }
        public abstract string? RefreshToken { get; protected set; }
        public abstract DateTime? AccessTokenExpiresAt { get; protected set; }

        public bool IsSessionActive => !string.IsNullOrEmpty(AccessToken);

        public async Task<IResult> LoginAsync(string username, string password)
        {
            var auth = await AuthenticateAsync(username, password);

            if (auth.IsFailure)
                return Result.Failure(auth.ErrorMessage);

            AccessToken = auth.AccessToken;
            RefreshToken = auth.RefreshToken;
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(auth.ExpiresInSeconds);

            await SaveSessionAsync();

            return Result.Success();
        }

        public async Task<IResult> TryRefreshSessionAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
                return Result.Failure("No refresh token available.");

            var refresh = await RefreshSessionAsync(RefreshToken!);
            if (refresh.IsFailure)
            {
                await ClearSessionAsync();
                return Result.Failure("Refresh failed");
            }

            AccessToken = refresh.AccessToken;
            RefreshToken = refresh.RefreshToken;
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(refresh.ExpiresInSeconds);

            await SaveSessionAsync();

            return Result.Success();
        }

        public async Task LogoutAsync()
        {
            AccessToken = null;
            RefreshToken = null;
            AccessTokenExpiresAt = null;
            await ClearSessionAsync();
        }

        public async Task<IResult> LoadSessionAsync()
        {
            var session = await LoadPersistedSessionAsync();
            if (session is not null)
            {
                AccessToken = session.AccessToken;
                RefreshToken = session.RefreshToken;
                AccessTokenExpiresAt = session.ExpiresAt;

                return Result.Success();
            }

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
