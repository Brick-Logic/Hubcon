using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    internal sealed class AuthenticationMiddleware(
        IAuthorizationService _authorizationService,
        ILogger<AuthenticationMiddleware> logger) : IAuthenticationMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            var user = context.HttpContext?.User;

            if ((context.Blueprint.Kind == OperationKind.Subscription) && user?.Identity?.IsAuthenticated != true)
            {
                logger.LogError($"Server: Subscriptions are required to be authenticated. Source IP: {context.HttpContext?.Connection.RemoteIpAddress}.");
                context.Result = new BaseOperationResponse<object>(false, "Access denied");
                context.HttpContext?.Connection.RequestClose();
                return;
            }

            if ((context.Blueprint.Kind == OperationKind.Stream) && user?.Identity?.IsAuthenticated != true)
            {
                logger.LogError($"Server: Authentication requerired. Source IP: {context.HttpContext?.Connection.RemoteIpAddress}.");
                context.Result = new BaseOperationResponse<object>(false, "Access denied");
                context.HttpContext?.Connection.RequestClose();
                return;
            }           

            if (context.Blueprint.RequiresAuthorization)
            {
                if (user?.Identity == null || !user.Identity.IsAuthenticated)
                {
                    context.Result = new BaseOperationResponse<object>(false, "Access denied");
                    return;
                }

                foreach (var attr in context.Blueprint.AuthorizationAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Policy))
                    {
                        var result = await _authorizationService.AuthorizeAsync(user, null, attr.Policy);
                        if (!result.Succeeded)
                        {
                            context.Result = new BaseOperationResponse<object>(false, "Access denied");
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(attr.Roles))
                    {
                        var roles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (!roles.Any(user.IsInRole))
                        {
                            context.Result = new BaseOperationResponse<object>(false, "Access denied");
                            return;
                        }
                    }
                }
            }

            await next();
        }
    }
}
