using HubconTestDomain;

namespace Hubcon.Blazor.Client.Auth
{
    public class AuthenticationManager(ISecondTestContract secondTestContract) : BaseAuthenticationManager
    {
        protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
        {
            var response = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand(username, password, true, new("525", new("525"))), "5"));
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

        protected async override Task<PersistedSession?> LoadPersistedSessionAsync()
        {
            var response = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand("username", "password", true, new("525", new("525"))), "5"));
            var loginResponse = response.Data!;
            
            return new PersistedSession()
            {
                AccessToken = loginResponse.AccessToken,
                TokenType = loginResponse.TokenType,
                RefreshToken = loginResponse.RefreshToken,
                ExpiresAt = loginResponse.Expires
            };
        }

        protected async override Task<IAuthResult> RefreshSessionAsync(string refreshToken)
        {
            var response = await secondTestContract.Execute(x => x.LoginAsync(new LoginCommand("username", "password", true, new("525", new("525"))), "5"));
            var loginResponse = response.Data!;
            
            return AuthResult.Success(
                loginResponse.AccessToken, 
                loginResponse.TokenType, 
                loginResponse.RefreshToken, 
                loginResponse.Expires);
        }

        protected async override Task SaveSessionAsync(PersistedSession session)
        {

        }
    }
}
