using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
#pragma warning disable CS1591


namespace Hubcon
{
    public static class JwtHelper
    {
        public static string? GetUserId(string? jwtToken)
        {
            if (jwtToken == null) return null;

            var jwtHandler = new JwtSecurityTokenHandler();

            if (!jwtHandler.CanReadToken(jwtToken))
                throw new UnauthorizedAccessException();

            JwtSecurityToken? token = jwtHandler.ReadJwtToken(jwtToken);
            var userIdClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim?.Value is null)
                return null;

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

        public static string? ExtractTokenFromHeader(string token)
        {
            try
            {
                if (token is null)
                    return null;

                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return token.Substring("Bearer ".Length).Trim();
                }

                return token;
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static ClaimsPrincipal? ValidateJwtToken(string token, TokenValidationParameters validationParameters, out SecurityToken? validatedToken)
        {
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var principal = handler.ValidateToken(token, validationParameters, out validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                validatedToken = null;
                return null;
            }
        }
    }
}
