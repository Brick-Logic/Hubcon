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
                var group = app.MapGroup(x);
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

        private static Delegate CreateDelegate(MethodInfo methodInfo, Type wrapperType, bool isGet = false)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));

            Type[] paramTypes;
            var parameters = methodInfo.GetParameters();
            if (parameters.Length > 0)
            {
                paramTypes = [wrapperType];
            }
            else
            {
                paramTypes = [.. parameters.Select(x => x.ParameterType)];
            }

            var (Instance, Method) = ProxyFactory.CreateProxyInstance(methodInfo, wrapperType, isGet);
            var returnType = methodInfo.ReturnType;

            if (paramTypes.Length > 16)
                throw new NotSupportedException("Métodos con más de 16 parámetros no soportados.");

            Type delegateType;

            if (returnType == typeof(void))
            {
                // Action<T1,...,Tn>
                delegateType = paramTypes.Length switch
                {
                    0 => typeof(Action),
                    1 => typeof(Action<>).MakeGenericType(paramTypes),
                    2 => typeof(Action<,>).MakeGenericType(paramTypes),
                    3 => typeof(Action<,,>).MakeGenericType(paramTypes),
                    4 => typeof(Action<,,,>).MakeGenericType(paramTypes),
                    5 => typeof(Action<,,,,>).MakeGenericType(paramTypes),
                    6 => typeof(Action<,,,,,>).MakeGenericType(paramTypes),
                    7 => typeof(Action<,,,,,,>).MakeGenericType(paramTypes),
                    8 => typeof(Action<,,,,,,,>).MakeGenericType(paramTypes),
                    9 => typeof(Action<,,,,,,,,>).MakeGenericType(paramTypes),
                    10 => typeof(Action<,,,,,,,,,>).MakeGenericType(paramTypes),
                    11 => typeof(Action<,,,,,,,,,,>).MakeGenericType(paramTypes),
                    12 => typeof(Action<,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    13 => typeof(Action<,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    14 => typeof(Action<,,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    15 => typeof(Action<,,,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    16 => typeof(Action<,,,,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    _ => throw new NotSupportedException()
                };
            }
            else
            {
                // Func<T1,...,Tn,TResult>
                Type[] typeArgs = paramTypes.Concat(new[] { returnType }).ToArray();
                delegateType = paramTypes.Length switch
                {
                    0 => typeof(Func<>).MakeGenericType(typeArgs),
                    1 => typeof(Func<,>).MakeGenericType(typeArgs),
                    2 => typeof(Func<,,>).MakeGenericType(typeArgs),
                    3 => typeof(Func<,,,>).MakeGenericType(typeArgs),
                    4 => typeof(Func<,,,,>).MakeGenericType(typeArgs),
                    5 => typeof(Func<,,,,,>).MakeGenericType(typeArgs),
                    6 => typeof(Func<,,,,,,>).MakeGenericType(typeArgs),
                    7 => typeof(Func<,,,,,,,>).MakeGenericType(typeArgs),
                    8 => typeof(Func<,,,,,,,,>).MakeGenericType(typeArgs),
                    9 => typeof(Func<,,,,,,,,,>).MakeGenericType(typeArgs),
                    10 => typeof(Func<,,,,,,,,,,>).MakeGenericType(typeArgs),
                    11 => typeof(Func<,,,,,,,,,,,>).MakeGenericType(typeArgs),
                    12 => typeof(Func<,,,,,,,,,,,,>).MakeGenericType(typeArgs),
                    13 => typeof(Func<,,,,,,,,,,,,,>).MakeGenericType(typeArgs),
                    14 => typeof(Func<,,,,,,,,,,,,,,>).MakeGenericType(typeArgs),
                    15 => typeof(Func<,,,,,,,,,,,,,,,>).MakeGenericType(typeArgs),
                    16 => typeof(Func<,,,,,,,,,,,,,,,,>).MakeGenericType(typeArgs),
                    _ => throw new NotSupportedException()
                };
            }

            return Delegate.CreateDelegate(delegateType, Instance, Method);
        }

        public static Delegate CreateHandler(MethodInfo methodInfo, Type wrapperType)
        {
            // Definimos el parámetro de entrada: (WrapperType wrapper)
            var wrapperParam = Expression.Parameter(wrapperType, "wrapper");

            // Obtenemos la instancia del servicio (se asume que se pasa o se obtiene de DI)
            // Para Minimal APIs, el primer parámetro suele ser el servicio
            var serviceParam = Expression.Parameter(methodInfo.DeclaringType!, "service");

            // Construimos los argumentos para llamar al método real: wrapper.Prop1, wrapper.Prop2...
            var args = methodInfo.GetParameters().Select(p =>
            {
                var propInfo = wrapperType.GetProperty(p.Name!);
                return Expression.Property(wrapperParam, propInfo!);
            });

            // Llamada al método: service.Method(wrapper.Prop1, ...)
            var methodCall = Expression.Call(serviceParam, methodInfo, args);

            // Convertimos el resultado a object (para manejar Task o void)
            var conversion = Expression.Convert(methodCall, typeof(object));

            // Creamos el delegado: (service, wrapper) => (object)service.Method(wrapper.Prop1, ...)
            return Expression.Lambda(conversion, serviceParam, wrapperParam).Compile();
        }
    }
}