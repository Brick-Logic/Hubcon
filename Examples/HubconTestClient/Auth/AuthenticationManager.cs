using Hubcon.Client.Authentication;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;

namespace HubconTestClient.Auth
{
    public class AuthenticationManager(ISecondTestContract secondTestContract) : BaseAuthenticationManager
    {
        public override string? AccessToken { get; protected set; } = "";
        public override string? RefreshToken { get; protected set; } = "";
        public override DateTime? AccessTokenExpiresAt { get; protected set; } = DateTime.UtcNow.AddYears(1);

        protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
        {
            var token = await secondTestContract.LoginAsync(username, password);

            AccessToken = token;
            RefreshToken = "";
            AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

            return AuthResult.Success(token, "", 100000);
        }

        protected override Task ClearSessionAsync()
        {
            AccessToken = "";
            RefreshToken = "";
            AccessTokenExpiresAt = null;

            return Task.CompletedTask;
        }

        protected async override Task<PersistedSession?> LoadPersistedSessionAsync()
        {
            var token = await secondTestContract.LoginAsync("", "");

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
