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
    public class JwtAuthHandler : IAuthHandler
    {
        public async ValueTask<ClaimsPrincipal?> AuthenticateAsync(IOperationContext context, IUseAuthAttribute originAttribute)
        {
            var token = JwtHelper.ExtractTokenFromHeader(context.HttpContext);
            var tokenValidationParameters = context.RequestServices.GetRequiredService<TokenValidationParameters>();

            var user = JwtHelper.ValidateJwtToken(token!, tokenValidationParameters, out var validatedToken);

            return user;
        }
    }
}