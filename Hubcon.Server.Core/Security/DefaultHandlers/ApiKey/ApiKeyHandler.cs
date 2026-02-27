using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public sealed class ApiKeyHandler : IAuthHandler
    {
        public ValueTask<ClaimsPrincipal?> AuthenticateAsync(IOperationContext context, IUseAuthAttribute originAttribute)
        {
            var attribute = originAttribute as UseApiKeyAttribute;

            if (context.HttpContext!.Request.Headers.TryGetValue("X-API-KEY", out var apiKey))
            {
                var key = apiKey.ToString();

                if(key == attribute!.Key)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, "APIKeyUser")
                    };

                    if(attribute.ShouldOverrideAuthorization)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "AuthOverride"));
                    }

                    var identity = new ClaimsIdentity(claims, "ApiKey");
                    var principal = new ClaimsPrincipal(identity);
                    return ValueTask.FromResult<ClaimsPrincipal?>(principal);
                }
            }

            return ValueTask.FromResult<ClaimsPrincipal?>(null);
        }
    }
}
