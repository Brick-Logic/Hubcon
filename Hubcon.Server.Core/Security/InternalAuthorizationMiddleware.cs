using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Security
{
    public class InternalAuthorizationMiddleware(IAuthorizationService authService) : IAuthenticationMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            if(!context.Blueprint.RequiresAuthorization)
            {
                await next();
                return;
            }

            var policy = context.Blueprint.SecurityPolicy;

            ClaimsPrincipal? principal = null;

            foreach (var attribute in policy.Handlers)
            {
                var handlerInstance = (context.RequestServices.GetRequiredService(attribute.HandlerType) as IAuthHandler)!;
                principal = await handlerInstance.AuthenticateAsync(context, attribute);

                if (principal != null) break;
            }

            if (principal == null)
            {
                SetUnauthorized(context);
                return;
            }

            var isAuthorized = principal.IsInRole("AuthOverride") || await CheckPermissionsAsync(principal, policy);

            if (!isAuthorized)
            {
                SetUnauthorized(context);
                return;
            }

            context.User = principal;

            await next();
        }

        private async Task<bool> CheckPermissionsAsync(ClaimsPrincipal user, CompiledSecurityPolicy policy)
        {
            // Validación de Roles (Fast Path)
            if (policy.Roles.Length > 0 && !policy.Roles.Any(user.IsInRole)) return false;

            // Validación de Policies (Usa IAuthorizationService)
            foreach (var policyName in policy.Policies)
            {
                var result = await authService.AuthorizeAsync(user, policyName);
                if (!result.Succeeded) return false;
            }

            return true;
        }

        private static void SetUnauthorized(IOperationContext context)
        {
            context.Response = HubconResponse.Unauthorized();
        }
    }
}
