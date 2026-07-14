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
                EndpointManager.GetDummyEndpointDelegate(blueprint.ControllerType, blueprint.ContractType, method);

            var endpointReturnType = endpointDelegate!.Method.ReturnType;

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

                        var operationRequest = new OperationRequest(operationName, simpleContractName);
                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.stream_init, transport,
                                operationRequest))
                        {
                            return new HttpHubconResult(HubconResponse.TooManyRequests(), endpointReturnType);
                        }

                        IWrapper? wrapper = null;
                        if (blueprint.ParameterWrapper != null)
                        {
                            if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper internalWrapper)
                            {
                                return new HttpHubconResult(HubconResponse.BadRequest(), endpointReturnType);
                            }

                            wrapper = internalWrapper;
                        }
                        
                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            cancellationToken) as IHubconResponse;

                        if (res!.Failure)
                        {
                            return new HttpHubconResult(HubconResponse.InternalError(), endpointReturnType);
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
                            return new HttpHubconResult(response, endpointReturnType);
                        }
                        
                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.stream_init, transport,
                                operationRequest))
                        {
                            return new HttpHubconResult(HubconResponse.TooManyRequests(), endpointReturnType);
                        }
                        
                        if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper wrapper)
                        {
                            return new HttpHubconResult(HubconResponse.BadRequest(), endpointReturnType);
                        }
                        
                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            cancellationToken) as IHubconResponse;

                        if (res!.Failure)
                        {
                            return new HttpHubconResult(HubconResponse.InternalError(), endpointReturnType);
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
                        
                        var operationRequest = new OperationRequest(operationName, simpleContractName);

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_invoke,
                                transport,
                                operationRequest))
                        {
                            return new HttpHubconResult(HubconResponse.TooManyRequests(), endpointReturnType);
                        }

                        IWrapper? wrapper = null;
                        if (blueprint.ParameterWrapper != null)
                        {
                            if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper internalWrapper)
                            {
                                return new HttpHubconResult(HubconResponse.BadRequest(), endpointReturnType);
                            }

                            wrapper = internalWrapper;
                        }

                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            cancellationToken);

                        return new HttpHubconResult(res, endpointReturnType);
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
                            return new HttpHubconResult(response, endpointReturnType);
                        }
                        
                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_invoke, transport, operationRequest))
                        {
                            return new HttpHubconResult(HubconResponse.TooManyRequests(), endpointReturnType);
                        }

                        if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper wrapper)
                        {
                            return new HttpHubconResult(HubconResponse.BadRequest(), endpointReturnType);
                        }
                        
                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            cancellationToken);

                        return new HttpHubconResult(res, endpointReturnType);
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

                        var operationRequest = new OperationRequest(operationName, simpleContractName);

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_call, transport,
                                operationRequest))
                        {
                            return new HttpHubconResult(HubconResponse.TooManyRequests(), endpointReturnType);
                        }
                        
                        IWrapper? wrapper = null;
                        if (blueprint.ParameterWrapper != null)
                        {
                            if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper internalWrapper)
                            {
                                return new HttpHubconResult(HubconResponse.BadRequest(), endpointReturnType);
                            }

                            wrapper = internalWrapper;
                        }

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            cancellationToken);

                        return new HttpHubconResult(res, endpointReturnType);
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
                            return new HttpHubconResult(response, endpointReturnType);
                        }
                        
                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_call, transport,
                                operationRequest))
                        {
                            return new HttpHubconResult(HubconResponse.TooManyRequests(), endpointReturnType);
                        }
                        
                        if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper wrapper)
                        {
                            return new HttpHubconResult(HubconResponse.BadRequest(), endpointReturnType);
                        }

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            cancellationToken);

                        return new HttpHubconResult(res, endpointReturnType);
                    });
                }
            }

            builder.WithRequestTimeout(options.HttpTimeout);
            builder.WithMetadata(new ProducesResponseTypeMetadata(400, typeof(IHubconResponse<Dictionary<string, string[]>>), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(403, typeof(IResponse), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(404, typeof(IResponse), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(500, typeof(IResponse), ["application/json"]));
            
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
    }
}