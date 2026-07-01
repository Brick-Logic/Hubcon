using Hubcon;
using HubconTestDomain;
using System;
using System.Threading.Tasks;

namespace HubconTestClient.Auth
{
    public class AuthenticationManager : BaseAuthenticationManager
    {
        private readonly ISecondTestContract secondTestContract;

        public AuthenticationManager(ISecondTestContract secondTestContract)
        {
            this.secondTestContract = secondTestContract;
        }

        protected override async Task<IAuthResult> AuthenticateAsync(string username, string password)
        {
            var token = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand(username, password, true)));
            var token2 = await secondTestContract.LoginAsync(new LoginCommand(username, password, true));
            return AuthResult.Success(token.Data!, "Bearer", RefreshToken!, DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
        }

        protected override Task<IAuthResult> AuthenticateWithTokenAsync(string token, string type)
        {
            throw new NotImplementedException();
        }

        protected override Task ClearSessionAsync()
        {           
            return Task.CompletedTask;
        }

        protected override async Task<PersistedSession?> LoadPersistedSessionAsync()
        {
            var token = await secondTestContract.LoginAsync(new LoginCommand("username", "password", true));

            return new PersistedSession()
            {
                TokenType = "Bearer",
                AccessToken = token,
                RefreshToken = "",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()
            };
        }

        protected override async Task<IAuthResult> RefreshSessionAsync(string refreshToken)
        {
            var token = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand("refresh", "password", true)));
            return AuthResult.Success(token!.Data, "Bearer", RefreshToken!, DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
        }

        protected override async Task SaveSessionAsync(PersistedSession session)
        {

        }
    }
}
