using Castle.Core.Logging;
using HotChocolate;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
{
    public class AuthenticationMiddleware(
        IAuthorizationService _authorizationService, 
        ILogger<AuthenticationMiddleware> logger) : IAuthenticationMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            var user = context.HttpContext?.User;

            //if ((context.Blueprint.Kind == OperationKind.Subscription || context.Blueprint.Kind == OperationKind.Stream) 
            //    && user?.Identity?.IsAuthenticated != true)
            //{
            //    logger.LogError($"Server: Subscriptions are required to be authenticated. Source IP: {context.HttpContext?.Connection.RemoteIpAddress}.");
            //    context.HttpContext?.Connection.RequestClose();
            //    return;
            //}

            if (context.Blueprint.RequiresAuthorization)
            {
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
