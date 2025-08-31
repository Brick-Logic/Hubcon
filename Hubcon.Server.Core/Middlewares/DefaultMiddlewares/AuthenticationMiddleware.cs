using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    public sealed class AuthenticationMiddleware(
        IAuthorizationService _authorizationService,
        ILogger<AuthenticationMiddleware> logger) : IAuthenticationMiddleware
    {
        private static readonly MemoryCache permissionRegistry = new(new MemoryCacheOptions());

        public bool TryGet(string tokenId, string permission, out bool isAllowed)
        {
            return permissionRegistry.TryGetValue((tokenId, permission), out isAllowed);
        }

        public void Set(string tokenId, string permission, bool isAllowed, TimeSpan ttl)
        {
            permissionRegistry.Set((tokenId, permission), isAllowed, ttl);
        }

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
                    context.Result = new BaseOperationResponse<object>(false, error: "Access denied");
                    return;
                }

                foreach (var attr in context.Blueprint.AuthorizationAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Policy))
                    {
                        if (!TryGet(context.HttpContext!.Request.Headers.Authorization!, attr.Policy, out bool isAllowed))
                        {
                            var result = await _authorizationService.AuthorizeAsync(user, null, attr.Policy);
                            if (!result.Succeeded)
                            {
                                context.Result = new BaseOperationResponse<object>(false, error: "Access denied");
                                Set(context.HttpContext!.Request.Headers.Authorization!, attr.Policy, false, TimeSpan.FromMinutes(15));
                                return;
                            }
                            else
                            {
                                Set(context.HttpContext!.Request.Headers.Authorization!, attr.Policy, true, TimeSpan.FromMinutes(15));
                            }
                        }
                        else if (!isAllowed)
                        {
                            context.Result = new BaseOperationResponse<object>(false, error: "Access denied");
                            return;
                        }
                        
                    }

                    if (!string.IsNullOrWhiteSpace(attr.Roles))
                    {
                        if(!TryGet(context.HttpContext!.Request.Headers.Authorization!, attr.Roles, out bool isAllowed))
                        {
                            var roles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (!roles.Any(user.IsInRole))
                            {
                                context.Result = new BaseOperationResponse<object>(false, error: "Access denied");
                                Set(context.HttpContext!.Request.Headers.Authorization!, attr.Roles, false, TimeSpan.FromMinutes(15));
                                return;
                            }
                            else
                            {
                                Set(context.HttpContext!.Request.Headers.Authorization!, attr.Roles, true, TimeSpan.FromMinutes(15));
                            }
                        }
                        else if (!isAllowed)
                        {
                            context.Result = new BaseOperationResponse<object>(false, error: "Access denied");
                            return;
                        }
                    }
                }
            }

            await next();
        }
    }
}
