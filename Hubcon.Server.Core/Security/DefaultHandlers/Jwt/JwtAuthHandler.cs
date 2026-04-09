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
    public class JwtAuthHandler(IOperationCache operationCache) : IAuthHandler
    {
        ///<inheritdoc/>
        public async ValueTask<ClaimsPrincipal?> AuthenticateAsync(IOperationContext context, IUseAuthAttribute originAttribute)
        {
            var token = JwtHelper.ExtractTokenFromHeader(context.HttpContext!.Request.Headers.Authorization.ToString());

            if (operationCache.TryGetValue(token!, out ClaimsPrincipal? cachedUser) && cachedUser != null)
            {
                return cachedUser;
            }

            var tokenValidationParameters = context.RequestServices.GetRequiredService<IInternalServerOptions>().TokenValidationParameters;
            var user = JwtHelper.ValidateJwtToken(token!, tokenValidationParameters!, out _);

            operationCache.Set(token!, user!, expirationMinutes:5);
            return user;
        }
    }
}