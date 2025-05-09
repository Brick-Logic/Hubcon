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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuthorizationService _authorizationService;
        private readonly IMethodDescriptorProvider _methodDescriptorProvider;

        public AuthenticationMiddleware(IHttpContextAccessor accessor, IAuthorizationService authService, IMethodDescriptorProvider methodDescriptorProvider)
        {
            _httpContextAccessor = accessor;
            _authorizationService = authService;
            _methodDescriptorProvider = methodDescriptorProvider;
        }

        public async Task<IObjectMethodResponse?> Execute(IMethodInvokeRequest request, InvocationDelegate next)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out IMethodDescriptor? value))
                return new BaseMethodResponse(false, "Bad request");

            if (value!.RequiresAuthorization)
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity == null || !user.Identity.IsAuthenticated)
                    return new BaseMethodResponse(false, "Access denied");

                var authorizeAttributes = value.InternalMethodInfo.GetCustomAttributes<AuthorizeAttribute>();

                foreach (var attr in authorizeAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Policy))
                    {
                        var result = await _authorizationService.AuthorizeAsync(user, null, attr.Policy);
                        if (!result.Succeeded)
                            return new BaseMethodResponse(false, "Access denied");
                    }

                    if (!string.IsNullOrWhiteSpace(attr.Roles))
                    {
                        var roles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (!roles.Any(user.IsInRole))
                            return new BaseMethodResponse(false, "Access denied");
                    }
                }
            }

            return await next();
        }
    }
}
