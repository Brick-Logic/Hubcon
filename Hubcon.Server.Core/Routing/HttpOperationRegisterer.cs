using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.Helpers;
using Hubcon.Server.Core.Routing.Models;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Websockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

#pragma warning disable CS1591

namespace Hubcon.Server.Core.Routing
{
    public static class HttpOperationRegisterer
    {
        private readonly static MethodInfo methodInfo1 =
            typeof(EndpointFilterExtensions).GetMethod("AddEndpointFilter", [typeof(RouteHandlerBuilder)])!;

        private readonly static ConcurrentDictionary<string, RouteGroupBuilder> EndpointGroups = new();
        private readonly static ConcurrentDictionary<RouteGroupBuilder, bool> RateLimiterApplied = new();


        public static void RegisterEndpoint(
            this WebApplication app,
            IOperationBlueprint blueprint)
        {
            if (!blueprint.SupportsTransport<HttpTransport>())
                return;

            IEndpointConventionBuilder builder = null!;
            var route = blueprint.HttpRoute!;
            var operationName = blueprint.OperationName;
            var simpleContractName = NamingHelper.GetCleanName(blueprint.ContractName);
            var options = app.Services.GetRequiredService<IInternalServerOptions>();
            var method = (MethodInfo)blueprint.MemberInfo!;
            
            var endpointDelegate =
                EndpointManager.GetDummyEndpointDelegate(blueprint.ContractName, method.GetMethodSignature());

            if (endpointDelegate == null)
                throw new HubconGenericException(
                    $"Could not find a suitable delegate for endpoint '{method.Name}', on contract '{blueprint.ContractName}'. This could mean the source generators had an error.");

            if (options.MethodOverloadingIsEnabled) route = $"{method.GetMethodSignature()}";

            var controllerMethod = blueprint.ControllerType.GetMethod(
                method.Name,
                method.GetParameters().Select(x => x.ParameterType).ToArray());

            var filters = controllerMethod!.GetCustomAttributes()
                .Where(x => x is UseHttpEndpointFilterAttribute)
                .Select(x => (UseHttpEndpointFilterAttribute)x)
                .ToList();

            var classFilters = blueprint.ControllerType.GetCustomAttributes()
                .Where(x => x is UseHttpEndpointFilterAttribute)
                .Select(x => (UseHttpEndpointFilterAttribute)x)
                .ToList();

            var orderedParameterNames = method
                .GetParameters()
                .Select(p => p.Name!)
                .ToArray();


            filters.AddRange(classFilters);

            var endpointGroup = EndpointGroups.GetOrAdd(blueprint.HttpEndpointGroupName!, x =>
            {
                var group = app
                    .MapGroup(x)
                    .WithTags(x);
                return group;
            });

            HttpMethod verbResult = blueprint.HttpVerb!;
            SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

            if (blueprint.Kind == OperationKind.Stream)
            {
                if (verbResult == HttpMethod.Get)
                {
                    builder = endpointGroup.MapGet(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        var dict = context.Request.Query
                            .ToDictionary(static kvp => kvp.Key, static object (kvp) => kvp.Value.ToString());

                        var operationRequest = new OperationRequest(operationName, simpleContractName, dict);
                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.stream_init, transport,
                                operationRequest))
                        {
                            return HubconResponse.TooManyRequests();
                        }

                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            transport,
                            services,
                            null,
                            cancellationToken);

                        if (res.Failure)
                        {
                            return ProcessResponse(res, context);
                        }

                        return new SseResult((res.Data as IAsyncEnumerable<object?>)!, operationRequest);
                    });
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            var response = HubconResponse.RequestTooLarge();
                            return ProcessResponse(response, context);
                        }

                        var args = new Dictionary<string, object>();

                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName,
                            args
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.stream_init, transport,
                                operationRequest))
                        {
                            return HubconResponse.TooManyRequests();
                        }

                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            transport,
                            services,
                            null,
                            cancellationToken);

                        if (res.Failure)
                        {
                            return ProcessResponse(res, context);
                        }

                        return new SseResult((res.Data! as IAsyncEnumerable<object?>)!, operationRequest);
                    });
                }
            }
            else if (blueprint.HasReturnType)
            {
                if (verbResult == HttpMethod.Get)
                {
                    builder = endpointGroup.MapGet(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        var dict = context.Request.Query
                            .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.ToString());

                        var operationRequest = new OperationRequest(operationName, simpleContractName, dict);

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_invoke,
                                transport,
                                operationRequest))
                        {
                            return HubconResponse.TooManyRequests();
                        }

                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            transport,
                            services,
                            null,
                            cancellationToken);

                        return res;
                    });
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var converter = services.GetRequiredService<IDynamicConverter>();
                        var cancellationToken = context.RequestAborted;

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            var response = HubconResponse.RequestTooLarge();
                            return ProcessResponse(response, context);
                        }

                        var args = new Dictionary<string, object>();

                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName,
                            args
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_invoke, transport,
                                operationRequest))
                        {
                            return HubconResponse.TooManyRequests();
                        }

                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            transport,
                            services,
                            null,
                            cancellationToken);

                        return res;
                    });
                }
            }
            else
            {
                if (verbResult == HttpMethod.Get)
                {
                    builder = endpointGroup.MapGet(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        var dict = new Dictionary<string, object>();

                        foreach (var kvp in context.Request.Query)
                        {
                            dict[kvp.Key] = kvp.Value.ToString();
                        }

                        var operationRequest = new OperationRequest(operationName, simpleContractName, dict);

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_call, transport,
                                operationRequest))
                        {
                            return HubconResponse.TooManyRequests();
                        }

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            transport,
                            services,
                            null,
                            cancellationToken);

                        return res;
                    });
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var converter = services.GetRequiredService<IDynamicConverter>();
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            var response = HubconResponse.RequestTooLarge();
                            return ProcessResponse(response, context);
                        }

                        var args = new Dictionary<string, object>();

                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName,
                            args
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_call, transport,
                                operationRequest))
                        {
                            return HubconResponse.TooManyRequests();
                        }

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            transport,
                            services,
                            null,
                            cancellationToken);

                        return res;
                    });
                }
            }

            builder
                .WithRequestTimeout(options.HttpTimeout);

            builder.AllowAnonymous();
            options.EndpointConventions?.Invoke(builder);
        }

        static void SetupEndpointGroup(
            IInternalServerOptions options,
            IEndpointConventionBuilder builder,
            RouteGroupBuilder endpointGroup,
            IOperationBlueprint blueprint,
            MethodInfo controllerMethod,
            List<UseHttpEndpointFilterAttribute> filters)
        {
            options.EndpointConventions?.Invoke(builder);

            if (!options.ThrottlingIsDisabled)
            {
                var limiterApplied = RateLimiterApplied.TryGetValue(endpointGroup, out var result);
                if (!limiterApplied)
                {
                    var ContractRateLimiter = blueprint.ControllerType
                        .GetCustomAttributes<UseHttpRateLimiterAttribute>().FirstOrDefault();
                    if (ContractRateLimiter != null)
                    {
                        endpointGroup.RequireRateLimiting(ContractRateLimiter.Policy);
                        RateLimiterApplied.TryAdd(endpointGroup, true);
                    }
                }

                var OperationRateLimiter = controllerMethod!.GetCustomAttributes<UseHttpRateLimiterAttribute>()
                    .FirstOrDefault();
                if (OperationRateLimiter != null)
                    builder.RequireRateLimiting(OperationRateLimiter.Policy);
            }

            options.RouteHandlerBuilderConfig?.Invoke((builder as RouteHandlerBuilder)!);
        }


        private static IHubconResponse<T> ProcessResponse<T>(IHubconResponse<T> hubconResponse, HttpContext context)
        {
            context.Response.StatusCode = hubconResponse.StatusCode;
            context.Response.ContentType = "application/json";
            return hubconResponse;
        }
    }
}