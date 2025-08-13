using Hubcon.Client.Core.Authentication;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;

namespace Hubcon.Blazor.Client.Auth
{
    public class AuthenticationManager(ISecondTestContract secondTestContract) : BaseAuthenticationManager
    {
        public override string? AccessToken { get; protected set; } = "";
        public override string? RefreshToken { get; protected set; } = "";
        public override DateTime? AccessTokenExpiresAt { get; protected set; } = DateTime.UtcNow.AddYears(1);
        public override string? TokenType { get; protected set; }

        protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
        {
            var token = await secondTestContract.LoginAsync(username, password);

            TokenType = "Bearer";
            AccessToken = token;
            RefreshToken = "";
            AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

            return AuthResult.Success(token, "", 100000);
        }

        protected override Task ClearSessionAsync()
        {
            TokenType = "";
            AccessToken = "";
            RefreshToken = "";
            AccessTokenExpiresAt = null;

            return Task.CompletedTask;
        }

        protected async override Task<PersistedSession?> LoadPersistedSessionAsync()
        {
            var token = await secondTestContract.LoginAsync("", "");

            TokenType = "Bearer";
            AccessToken = token;
            RefreshToken = "";
            AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);


            return new PersistedSession()
            {
                AccessToken = token,
                RefreshToken = ""
            };
        }

        protected async override Task<IAuthResult> RefreshSessionAsync(string refreshToken)
        {
            var token = await secondTestContract.LoginAsync(Username, Password);

            TokenType = "Bearer";
            AccessToken = token;
            RefreshToken = "";
            AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

            return AuthResult.Success(token, "", 100000);
        }

        protected async override Task SaveSessionAsync()
        {
            
        }
    }
}
