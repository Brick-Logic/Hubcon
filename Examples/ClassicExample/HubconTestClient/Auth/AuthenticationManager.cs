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
            var response = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand(username, password, true, null), "5"));
            var loginResponse = response.Data!;
            
            return AuthResult.Success(
                loginResponse.AccessToken, 
                loginResponse.TokenType, 
                loginResponse.RefreshToken, 
                loginResponse.Expires);
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
            var response = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand("username", "password", true, null), "5"));
            var loginResponse = response.Data!;
            
            return new PersistedSession()
            {
                AccessToken = loginResponse.AccessToken,
                TokenType = loginResponse.TokenType,
                RefreshToken = loginResponse.RefreshToken,
                ExpiresAt = loginResponse.Expires
            };
        }

        protected override async Task<IAuthResult> RefreshSessionAsync(string refreshToken)
        {
            var response = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand("username", "password", true, null), "5"));
            var loginResponse = response.Data!;
            
            return AuthResult.Success(
                loginResponse.AccessToken, 
                loginResponse.TokenType, 
                loginResponse.RefreshToken, 
                loginResponse.Expires);
        }

        protected override async Task SaveSessionAsync(PersistedSession session)
        {

        }
    }
}
