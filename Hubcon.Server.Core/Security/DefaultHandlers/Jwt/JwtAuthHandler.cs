using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Hubcon's JWT token authentication handler.
    /// </summary>
    public class JwtAuthHandler : IAuthHandler
    {
        ///<inheritdoc/>
        public async ValueTask<ClaimsPrincipal?> AuthenticateAsync(IOperationContext context, IUseAuthAttribute originAttribute)
        {
            var token = JwtHelper.ExtractTokenFromHeader(context.HttpContext);
            var tokenValidationParameters = context.RequestServices.GetRequiredService<IInternalServerOptions>().TokenValidationParameters;

            if (tokenValidationParameters == null) 
                throw new ArgumentException("Hubcon's built-in JWT authentication handler needs a registered TokenValidationParameter object to work properly. Please, use WebApplicationBuilder.AddHuconServer(serverOptions => serverOptions.UseTokenValidationParameters(tokenValidationParameters)) in your program.cs to configure it.");

            var user = JwtHelper.ValidateJwtToken(token!, tokenValidationParameters, out _);
            return user;
        }
    }
}