using Hubcon.Client.Core.Authentication;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;

namespace HubconTestClient.Auth
{
    public class AuthenticationManager(ISecondTestContract secondTestContract) : BaseAuthenticationManager
    {
        protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
        {
            var token = await secondTestContract.LoginAsync(new LoginCommand(username, password, true));
            return AuthResult.Success(token, "Bearer", RefreshToken, AccessTokenExpiresAt!.Value);
        }

        protected override Task<IAuthResult> AuthenticateWithTokenAsync(string token, string type)
        {
            throw new NotImplementedException();
        }

        protected override Task ClearSessionAsync()
        {
            

            return Task.CompletedTask;
        }

        protected async override Task<PersistedSession?> LoadPersistedSessionAsync()
        {
            var token = await secondTestContract.LoginAsync(new LoginCommand("username", "password", true));

            return new PersistedSession()
            {
                TokenType = "Bearer",
                AccessToken = token,
                RefreshToken = "",
                ExpiresAt = DateTime.UtcNow.AddYears(1)
            };
        }

        protected async override Task<IAuthResult> RefreshSessionAsync(string refreshToken)
        {
            var token = await secondTestContract.LoginAsync(new LoginCommand("username", "password", true));
            return AuthResult.Success(token, "Bearer", "", DateTime.UtcNow.AddYears(1));
        }

        protected async override Task SaveSessionAsync(PersistedSession session)
        {

        }
    }
}
