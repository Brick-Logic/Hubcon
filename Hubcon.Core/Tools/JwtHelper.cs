using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Tools
{
    public static class JwtHelper
    {
        public static string? GetUserId(string? jwtToken)
        {
            if(jwtToken == null) return null;

            var jwtHandler = new JwtSecurityTokenHandler();

            if (!jwtHandler.CanReadToken(jwtToken))
                throw new UnauthorizedAccessException();

            JwtSecurityToken? token = jwtHandler.ReadJwtToken(jwtToken);
            var userIdClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim?.Value is not null)
                throw new UnauthorizedAccessException("No se encontró el ID de usuario en el token.");

            return userIdClaim?.Value;
        }

        public static string? ExtractTokenFromHeader(HttpContext? httpContext)
        {
            try
            {
                if (httpContext is null)
                    return null;

                var authHeader = httpContext.Request.Headers["Authorization"].ToString();

                if (authHeader is null)
                    return null;

                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring("Bearer ".Length).Trim();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
