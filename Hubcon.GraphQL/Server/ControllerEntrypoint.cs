using Autofac;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Data;
using Hubcon.GraphQL.Models.CustomAttributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Server
{
    public class ControllerEntrypoint : IHubconEntrypoint
    {
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        [HubconMethod(MethodType.Mutation)]
        public async Task<BaseJsonResponse> HandleMethodTask(MethodInvokeRequest request)
            => await HubconController.Pipeline.HandleWithResultAsync(request);

        [HubconMethod(MethodType.Mutation)]
        public async Task<IResponse> HandleMethodVoid(MethodInvokeRequest request)
            => await HubconController.Pipeline.HandleWithoutResultAsync(request);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest request)
            => HubconController.Pipeline.GetStream(request);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleSubscription(SubscriptionRequest request)
        {
            var accessor = ServiceProvider.ResolveOptional<IHttpContextAccessor>();

            string? userId = "broadcast";
            string? jwtToken = ExtractTokenFromHeader(accessor?.HttpContext!);

            if (jwtToken is not null)
            {
                var jwtHandler = new JwtSecurityTokenHandler();

                if (!jwtHandler.CanReadToken(jwtToken))
                    throw new UnauthorizedAccessException();

                JwtSecurityToken? token = jwtHandler.ReadJwtToken(jwtToken);
                var userIdClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

                if (userIdClaim?.Value is not null)
                    throw new UnauthorizedAccessException("No se encontró el ID de usuario en el token.");

                userId = userIdClaim?.Value ?? "broadcast";
            }

            return HubconController.Pipeline.GetSubscription(request, userId);
        }

        static string? ExtractTokenFromHeader(HttpContext? httpContext)
        {
            try
            {
                if (httpContext is null)
                    return null;

                var authHeader = httpContext.Request.Headers["Authorization"].ToString();

                if (authHeader is null)
                    return null;

                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring("Bearer ".Length).Trim();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public void Build(WebApplication? app = null)
        {
        }
    }
}