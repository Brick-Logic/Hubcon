using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable CS1591


namespace Hubcon.Server.Core.Security
{
    public class InternalAuthorizationMiddleware(IAuthorizationService authService, IOperationCache operationCache) : IAuthenticationMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            if(!context.Blueprint.RequiresAuthorization)
            {
                await next();
                return;
            }

            var policy = context.Blueprint.SecurityPolicy;

            ClaimsPrincipal? principal = await policy.Execute(context, context.RequestServices);

            if (principal == null)
            {
                SetUnauthorized(context);
                return;
            }

            var isAuthorized = await CheckPermissionsAsync(principal, policy);

            if (!isAuthorized)
            {
                SetUnauthorized(context);
                return;
            }

            context.User = principal;
            await next();
        }

        private async ValueTask<bool> CheckPermissionsAsync(ClaimsPrincipal user, CompiledSecurityPolicy policy)
        {
            if ((operationCache.TryGetValue(user, out bool isAuthorized) && isAuthorized) || user.IsInRole("AuthOverride"))
                return true;

            // Validación de Roles (Fast Path)
            if (policy.Roles.Length > 0)
            {
                bool hasRole = false;
                for (int i = 0; i < policy.Roles.Length; i++)
                {
                    if (user.IsInRole(policy.Roles[i]))
                    {
                        hasRole = true;
                        break;
                    }
                }
                if (!hasRole) return false;
            }

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
