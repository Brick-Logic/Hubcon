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
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Core.Extensions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

#pragma warning disable CS1591

namespace Hubcon.Server.Core.Routing
{
    public static class HttpOperationRegisterer
    {
        private static readonly ConcurrentDictionary<string, RouteGroupBuilder> EndpointGroups = new();
        private static readonly ConcurrentDictionary<RouteGroupBuilder, bool> RateLimiterApplied = new();

        public static void RegisterEndpoint(
            this WebApplication app,
            IOperationBlueprint blueprint)
        {
            if (!blueprint.SupportsTransport<HttpTransport>())
                return;

            IEndpointConventionBuilder builder = null!;

            var operationName = blueprint.OperationName;
            var simpleContractName = NamingHelper.GetCleanName(blueprint.ContractName);
            var options = app.Services.GetRequiredService<IInternalServerOptions>();
            var method = (MethodInfo)blueprint.MemberInfo!;
            var transportAttribute = HubconTransportAttribute.GetDefault<HttpTransport>();

            var httpSettings = options.GetTransportSettings(transportAttribute);
                
            var combinedRoute = method.GetRoute(httpSettings.MethodOverloadingEnabled);
            var route = httpSettings.TransportPrefix + combinedRoute.Endpoint;
            var endpointGroupName = combinedRoute.EndpointGroup;
            
            var endpointDelegate =
                EndpointManager.GetDummyEndpointDelegate(blueprint.ControllerType, blueprint.ContractType, method);

            var endpointReturnType = endpointDelegate!.Method.ReturnType;

            if (endpointDelegate == null)
                throw new HubconGenericException(
                    $"Could not find a suitable delegate for endpoint '{method.Name}', on contract '{blueprint.ContractName}'. This could mean the source generators had an error.");

            if (httpSettings.MethodOverloadingEnabled) route = $"{method.GetMethodSignature()}";

            var controllerMethod = blueprint.ControllerType.GetMethod(
                method.Name,
                method.GetParameters().Select(x => x.ParameterType).ToArray());

            var filters = controllerMethod!.GetCustomAttributes()
                .OfType<UseHttpEndpointFilterAttribute>()
                .ToList();

            var classFilters = blueprint.ControllerType.GetCustomAttributes()
                .OfType<UseHttpEndpointFilterAttribute>()
                .ToList();
            
            filters.AddRange(classFilters);

            var endpointGroup = EndpointGroups.GetOrAdd(endpointGroupName!, x =>
            {
                var group = app
                    .MapGroup(x)
                    .WithTags(x);
                
                return group;
            });

            HttpMethod verbResult = blueprint.HttpVerb!;

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
                        var requestId = context.GetOrCreateRequestId();

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = httpSettings.MaxMessageSizeInBytes;

                        var operationRequest = new OperationRequest(operationName, simpleContractName);
                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();
                        
                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.stream_init, transport,
                                operationRequest))
                        {
                            return HubconResponse.StatusTooManyRequests;
                        }

                        IWrapper? wrapper = null;
                        if (blueprint.ParameterWrapper != null)
                        {
                            if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper internalWrapper)
                            {
                                return HubconResponse.StatusBadRequest;
                            }

                            wrapper = internalWrapper;
                        }
                        
                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            requestId,
                            cancellationToken) as IHubconResponse;

                        if (res!.Failure)
                        {
                            return HubconResponse.StatusInternalError;
                        }

                        return new SseResult((res.Data as IAsyncEnumerable<object?>)!, operationRequest);
                    }).WithRequestTimeout(httpSettings.StreamOperationTimeout);
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;
                        var requestId = context.GetOrCreateRequestId();

                        if (context.Request.ContentLength > httpSettings.MaxMessageSizeInBytes)
                        {
                            return HubconResponse.StatusRequestTooLarge;
                        }
                        
                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName
                        );

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();
                        
                        var connectionLimiter = services.GetRequiredService<IConnectionLimiter>();
                        if (!connectionLimiter.TryAcquire(context.Connection.RemoteIpAddress!, transport))
                        {
                            return HubconResponse.StatusTooManyRequests;
                        }

                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.stream_init, transport,
                                operationRequest))
                        {
                            return HubconResponse.StatusTooManyRequests;
                        }
                        
                        if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper wrapper)
                        {
                            return HubconResponse.StatusBadRequest;
                        }
                        
                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            requestId,
                            cancellationToken) as IHubconResponse;

                        if (res!.Failure)
                        {
                            return HubconResponse.StatusInternalError;
                        }

                        return new SseResult((res.Data! as IAsyncEnumerable<object?>)!, operationRequest);
                    }).WithRequestTimeout(httpSettings.StreamOperationTimeout);
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
                        var requestId = context.GetOrCreateRequestId();

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = httpSettings.MaxMessageSizeInBytes;
                        
                        var operationRequest = new OperationRequest(operationName, simpleContractName);

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_invoke,
                                transport,
                                operationRequest))
                        {
                            return HubconResponse.StatusTooManyRequests;
                        }

                        IWrapper? wrapper = null;
                        if (blueprint.ParameterWrapper != null)
                        {
                            if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper internalWrapper)
                            {
                                return HubconResponse.StatusBadRequest;
                            }

                            wrapper = internalWrapper;
                        }

                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            requestId,
                            cancellationToken);

                        return res.GetOriginal();
                    }).WithRequestTimeout(httpSettings.InvokeOperationTimeout);
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;
                        var requestId = context.GetOrCreateRequestId();

                        if (context.Request.ContentLength > httpSettings.MaxMessageSizeInBytes)
                        {
                            return HubconResponse.StatusRequestTooLarge;
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
                            return HubconResponse.StatusTooManyRequests;
                        }

                        if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper wrapper)
                        {
                            return HubconResponse.StatusBadRequest;
                        }
                        
                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            requestId,
                            cancellationToken);

                        return res.GetOriginal();
                    }).WithRequestTimeout(httpSettings.InvokeOperationTimeout);
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
                        var requestId = context.GetOrCreateRequestId();
                        
                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = httpSettings.MaxMessageSizeInBytes;

                        if (context.Request.ContentLength > httpSettings.MaxMessageSizeInBytes)
                        {
                            return HubconResponse.StatusRequestTooLarge;
                        }

                        var operationRequest = new OperationRequest(operationName, simpleContractName);

                        var transport = HubconTransportAttribute.GetDefault<HttpTransport>();

                        var rateLimiter = services.GetRequiredService<IGlobalRateLimiterManager>();
                        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        if (!await rateLimiter.TryAcquireAsync(remoteAddress, MessageType.operation_call, transport,
                                operationRequest))
                        {
                            return HubconResponse.StatusTooManyRequests;
                        }
                        
                        IWrapper? wrapper = null;
                        if (blueprint.ParameterWrapper != null)
                        {
                            if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper internalWrapper)
                            {
                                return HubconResponse.StatusBadRequest;
                            }

                            wrapper = internalWrapper;
                        }

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            requestId,
                            cancellationToken);

                        return res.GetOriginal();
                    }).WithRequestTimeout(httpSettings.CallOperationTimeout);
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);
                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;
                        var requestId = context.GetOrCreateRequestId();

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = httpSettings.MaxMessageSizeInBytes;

                        if (context.Request.ContentLength > httpSettings.MaxMessageSizeInBytes)
                        {
                            return HubconResponse.StatusRequestTooLarge;
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
                            return HubconResponse.StatusTooManyRequests;
                        }
                        
                        if (invocationContext.Arguments.Count == 0 || invocationContext.Arguments.FirstOrDefault() is not IWrapper wrapper)
                        {
                            return HubconResponse.StatusBadRequest;
                        }

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            transport,
                            services,
                            wrapper,
                            requestId,
                            cancellationToken);

                        return res.GetOriginal();
                    }).WithRequestTimeout(httpSettings.CallOperationTimeout);
                }
            }

            builder.WithMetadata(new ProducesResponseTypeMetadata(400, typeof(IHubconResponse<Dictionary<string, string[]>>), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(401, typeof(IResponse), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(403, typeof(IResponse), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(404, typeof(IResponse), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(429, typeof(IResponse), ["application/json"]));
            builder.WithMetadata(new ProducesResponseTypeMetadata(500, typeof(IResponse), ["application/json"]));
            
            if(httpSettings.AllowAnonymousClients)
                builder.AllowAnonymous();
            
            options.EndpointConventions?.Invoke(builder);
            options.RouteHandlerBuilderConfig?.Invoke((builder as RouteHandlerBuilder)!);
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

            // if (!options.ThrottlingIsDisabled)
            // {
            //     var limiterApplied = RateLimiterApplied.TryGetValue(endpointGroup, out var result);
            //     if (!limiterApplied)
            //     {
            //         var ContractRateLimiter = blueprint.ControllerType
            //             .GetCustomAttributes<UseHttpRateLimiterAttribute>().FirstOrDefault();
            //         if (ContractRateLimiter != null)
            //         {
            //             endpointGroup.RequireRateLimiting(ContractRateLimiter.Policy);
            //             RateLimiterApplied.TryAdd(endpointGroup, true);
            //         }
            //     }
            //
            //     var OperationRateLimiter = controllerMethod!.GetCustomAttributes<UseHttpRateLimiterAttribute>()
            //         .FirstOrDefault();
            //     if (OperationRateLimiter != null)
            //         builder.RequireRateLimiting(OperationRateLimiter.Policy);
            // }

            options.RouteHandlerBuilderConfig?.Invoke((builder as RouteHandlerBuilder)!);
        }
    }
}