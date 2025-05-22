using Hubcon.Core.Abstractions.Standard.Attributes;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using HubconTestDomain;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HubconTest.Controllers
{
    public class SecondTestController(ILogger<SecondTestController> logger) : ISecondTestContract
    {
        public async Task TestMethod()
        {
            logger.LogInformation("TestMethod called");
        }

        public async Task TestMethod(string message)
        {
            logger.LogInformation(message);
        }

        public async Task<string> TestReturn(string message)
        {
            logger.LogInformation(message);
            return message;
        }

        public void TestVoid()
        {
            logger.LogInformation("TestVoid called.");
        }

        public string TestReturn()
        {
            logger.LogInformation("TestVoid called.");
            return "some return value";
        }

        public async Task<string> LoginAsync(string username, string password)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, username),
                    new Claim(ClaimTypes.Role, "Manager"),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };


                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Program.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: "clave",
                    audience: "clave",
                    claims: claims,
                    expires: DateTime.Now.AddYears(-1),
                    signingCredentials: creds);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                throw;
            }

        }
    }
}