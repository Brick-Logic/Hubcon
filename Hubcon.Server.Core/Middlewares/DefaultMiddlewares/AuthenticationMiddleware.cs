using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    public sealed class AuthenticationMiddleware(IAuthorizationService _authService, ILogger<AuthenticationMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            var httpContext = context.HttpContext;
            var user = httpContext?.User;

            // 2. Si no requiere autorización, seguimos sin tocar nada
            if (!context.Blueprint.RequiresAuthorization)
            {
                await next();
                return;
            }

            // 3. Chequeo de Políticas (Zero-allocation loop)
            // Usamos ReadOnlySpan o listas precomputadas del blueprint
            var policies = context.Blueprint.PrecomputedPolicies;
            for (int i = 0; i < policies.Length; i++)
            {
                var authResult = await _authService.AuthorizeAsync(user!, null, policies[i]);
                if (!authResult.Succeeded)
                {
                    SetUnauthorized(context);
                    return;
                }
            }

            // 4. Chequeo de Roles (Zero-allocation loop)
            var roles = context.Blueprint.PrecomputedRoles;
            foreach (var role in roles)
            {
                if (!user!.IsInRole(role))
                {
                    SetUnauthorized(context);
                    return;
                }
            }

            await next();
        }

        private static void SetUnauthorized(IOperationContext context)
        {
            context.Response = HubconResponse.Unauthorized();
        }
    }
}
