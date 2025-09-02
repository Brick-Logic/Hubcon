using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace HubconTest.Middlewares
{
    public class TestLoggingMiddleware(Lazy<IAuthorizationService> _authorizationService, ILogger<TestLoggingMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            var user = context.HttpContext?.User;

            if ((context.Blueprint.Kind == OperationKind.Subscription || context.Blueprint.Kind == OperationKind.Stream) && user?.Identity?.IsAuthenticated != true)
            {
                logger.LogError($"Server: Subscriptions are required to be authenticated. Source IP: {context.HttpContext?.Connection.RemoteIpAddress}.");
                context.Result = new BaseOperationResponse<object>(false, "Access denied");
                context.HttpContext?.Connection.RequestClose();
                return;
            }

            bool allowed = true;

            if(context.Blueprint.RequiresAuthorization)
            {
                var localCache = new Dictionary<string, bool>();

                foreach (var policy in context.Blueprint.PrecomputedPolicies)
                {
                    allowed &= await CheckPolicyCached(user, policy, localCache);
                }

                foreach (var role in context.Blueprint.PrecomputedRoles)
                {
                    allowed &= CheckRoleCached(user, role, localCache);
                }
            }

            if (!allowed)
            {
                context.Result = new BaseOperationResponse<object>(false, "Access denied");
                return;
            }

            await next();
        }

        private static readonly MemoryCache GlobalAuthCache = new MemoryCache(new MemoryCacheOptions());

        private async ValueTask<bool> CheckPolicyCached(ClaimsPrincipal user, string policy, Dictionary<string, bool> localCache)
        {
            localCache.TryGetValue(policy, out bool localResult);

            string key = $"{user.Identity!.Name}:{policy}";
            bool globalResult = !GlobalAuthCache.TryGetValue(key, out bool cachedGlobal) || cachedGlobal;

            bool coldResult = (localResult || globalResult) || (await _authorizationService.Value.AuthorizeAsync(user, null, policy)).Succeeded;

            bool result = localResult | globalResult | coldResult;

            localCache[policy] = result;

            if (!(localResult || globalResult))
                GlobalAuthCache.Set(key, result, TimeSpan.FromMinutes(1));

            return result;
        }

        private static bool CheckRoleCached(ClaimsPrincipal user, string role, Dictionary<string, bool> localCache)
        {
            localCache.TryGetValue(role, out bool localResult);

            bool result = localResult | user.IsInRole(role);

            localCache[role] = result;
            return result;
        }
    }
}
