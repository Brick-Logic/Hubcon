using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
{
    public class AuthenticationMiddleware : IPreRequestMiddleware
    {
        private readonly IAuthorizationService _authorizationService;

        public AuthenticationMiddleware(IAuthorizationService authService)
        {
            _authorizationService = authService;
        }

        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            if (context.Blueprint.RequiresAuthorization)
            {
                var user = context.HttpContext!.User;

                if (user?.Identity == null || !user.Identity.IsAuthenticated)
                {
                    context.Result = new BaseOperationResponse(false, "Access denied");
                    return;
                }

                foreach (var attr in context.Blueprint.AuthorizationAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Policy))
                    {
                        var result = await _authorizationService.AuthorizeAsync(user, null, attr.Policy);
                        if (!result.Succeeded)
                        {
                            context.Result = new BaseOperationResponse(false, "Access denied");
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(attr.Roles))
                    {
                        var roles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (!roles.Any(user.IsInRole))
                        {
                            context.Result = new BaseOperationResponse(false, "Access denied");
                            return;
                        }
                    }
                }
            }

            await next();
        }
    }
}
