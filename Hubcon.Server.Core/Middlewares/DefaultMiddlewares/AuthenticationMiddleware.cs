using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    //public sealed class AuthenticationMiddleware(IAuthorizationService _authService, ILogger<AuthenticationMiddleware> logger) : ILoggingMiddleware
    //{
    //    public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
    //    {
    //        var user = context?.User;

    //        if (!context!.Blueprint.RequiresAuthorization)
    //        {
    //            await next();
    //            return;
    //        }
        
    //        if (user == null || user.Identity?.IsAuthenticated == false)
    //        {
    //            SetUnauthorized(context);
    //            return;
    //        }

    //        var policies = context.Blueprint.PrecomputedPolicies;
    //        for (int i = 0; i < policies.Length; i++)
    //        {
    //            var authResult = await _authService.AuthorizeAsync(user!, null, policies[i]);
    //            if (!authResult.Succeeded)
    //            {
    //                SetUnauthorized(context);
    //                return;
    //            }
    //        }

    //        var roles = context.Blueprint.PrecomputedRoles;
    //        foreach (var role in roles)
    //        {
    //            if (!user!.IsInRole(role))
    //            {
    //                SetUnauthorized(context);
    //                return;
    //            }
    //        }

    //        await next();
    //    }

    //    private static void SetUnauthorized(IOperationContext context)
    //    {
    //        context.Response = HubconResponse.Unauthorized();
    //    }
    //}
}
