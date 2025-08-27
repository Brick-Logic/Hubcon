using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using HubconTestDomain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HubconTest.ContractHandlers
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
        //[EndpointDescription("Endpoint para crear un nuevo usuario en el sistema")]
        //[ProducesResponseType(400)]
        //[ProducesResponseType(500)]
        //[ProducesResponseType<IOperationResponse<string>>(200)]
        //[Consumes("application/json")]
        [AllowAnonymous]
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
                    expires: DateTime.Now.AddMilliseconds(300000),
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