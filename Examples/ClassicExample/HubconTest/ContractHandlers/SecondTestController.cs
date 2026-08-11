using Hubcon;
using HubconTestDomain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis.Validation;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace HubconTest.ContractHandlers
{
    public class SecondTestController(ILogger<SecondTestController> logger) : ISecondTestContract
    {
        public async Task TestMethod([Required] string message)
        {
            logger?.LogInformation(message);
        }

        public async Task<string> TestReturnWithParameter(string message)
        {
            logger?.LogInformation(message);
            return message;
        }

        public async Task<HubconResponse<bool>> TestHubconResponse()
        {
            var response = HubconResponse.OkT(true);
            return response;
        }

        public async Task TestVoid()
        {
            //logger.LogInformation("TestVoid called.");
        }

        public async Task<string> TestReturn()
        {
            //logger.LogInformation("TestVoid called.");
            return "some return value";
        }

        //[EndpointName("CreateUser")]
        //[EndpointSummary("Crear un nuevo usuario")]
        [EndpointDescription("Endpoint para crear un nuevo usuario en el sistema")]
        //[ProducesResponseType(400)]
        //[ProducesResponseType(500)]
        //[ProducesResponseType<IOperationResponse<string>>(200)]
        //[Consumes("application/json")]
        [AllowAnonymous]
        public async Task<LoginResponse> LoginAsync([Required][FromBody][ValidateObject] LoginCommand command, [FromQuery] string id)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, command.Username),
                    new Claim(ClaimTypes.Role, "Manager"),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };
                
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Program.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var expiration = DateTimeOffset.UtcNow.AddMinutes(30);
                
                var token = new JwtSecurityToken(
                    issuer: "clave",
                    audience: "clave",
                    claims: claims,
                    expires: expiration.DateTime,
                    signingCredentials: creds);

                var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);
                
                var response = new LoginResponse(jwtToken, "Bearer", "RefreshToken", expiration.ToUnixTimeSeconds());
                return response;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}