using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Helpers;
using Hubcon.Server.Core.Middlewares;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace Hubcon.Server.Core.Routing
{
    public static class HttpOperationRegisterer
    {
        private readonly static MethodInfo methodInfo = typeof(EndpointFilterExtensions).GetMethod("AddEndpointFilter", [typeof(RouteHandlerBuilder)])!;

        private readonly static ConcurrentDictionary<string, RouteGroupBuilder> EndpointGroups = new();
        private readonly static ConcurrentDictionary<RouteGroupBuilder, bool> RateLimiterApplied = new();

        public static void MapTypedEndpoint(
            this WebApplication app,
            IOperationBlueprint blueprint)
        {
            var generic = typeof(HttpOperationRegisterer)
                .GetMethod(nameof(RegisterEndpoint), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(blueprint.HasReturnType ? blueprint.ReturnType : typeof(IResponse));

            generic.Invoke(null, [app, blueprint]);
        }

        private static void RegisterEndpoint<TResponse>(
            WebApplication app,
            IOperationBlueprint blueprint)
        {
            var route = blueprint.Route;
            var operationName = blueprint.OperationName;
            var contractName = blueprint.ContractName;
            var options = app.Services.GetRequiredService<IInternalServerOptions>();
            RouteHandlerBuilder builder = null!;
            var method = (MethodInfo)blueprint.OperationInfo!;

            var controllerMethod = blueprint.ControllerType.GetMethod(
                method.Name,
                method.GetParameters().Select(x => x.ParameterType).ToArray());

            var returnType = typeof(IOperationResponse<>).MakeGenericType(blueprint.ReturnType);

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

            var endpointGroup = EndpointGroups.GetOrAdd(blueprint.HttpEndpointGroupName, x =>
            {
                var group = app.MapGroup(x);
                return group;
            });

            var verb = method.GetCustomAttribute<GetMethodAttribute>();

            if (verb != null && !method.AreParametersValid())
            {
                throw new InvalidOperationException($"Operation '{method.Name}' cannot be used with GET verb as it contains complex or null types. Use primitive types or a DTO class with primitive types instead.");
            }

            var verbResult = verb != null 
                ? HttpMethod.Get 
                : (blueprint.ParameterTypes.Count - blueprint.ParameterTypes.Count(x => x.Value == typeof(CancellationToken)) > 0 ? HttpMethod.Post : HttpMethod.Get);

            var endpointDelegate = CreateDelegate(controllerMethod!);

            if (blueprint.HasReturnType)
            {
                if (verbResult == HttpMethod.Get)
                {
                    builder = endpointGroup.MapGet(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var requestHandler = services.GetRequiredService<IRequestHandler>();
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        // No necesitamos revisar content length porque no hay body

                        var operationRequest = new OperationRequest(operationName, contractName);

                        // Parsear argumentos desde query string
                        foreach (var kvp in context.Request.Query)
                        {
                            // kvp.Key = nombre del argumento
                            // kvp.Value = valor del argumento (StringValues)
                            // Convertir a string y agregar al request
                            // Si tu OperationRequest tiene un diccionario de argumentos
                            operationRequest.Arguments[kvp.Key] = kvp.Value.ToString();
                        }

                        var res = await requestHandler.HandleWithResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            var errorMessage = options.DetailedErrorsEnabled
                                ? res.Error ?? "Internal error"
                                : "Internal error";

                            await InternalServerError(context);
                            return new BaseOperationResponse(false, errorMessage);
                        }

                        await Ok(context);
                        return res;
                    }).ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                }
                else
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var requestHandler = services.GetRequiredService<IRequestHandler>();
                        var converter = services.GetRequiredService<IDynamicConverter>();
                        var cancellationToken = context.RequestAborted;

                        //var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        //mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            await RequestTooLarge(context);
                            return new BaseOperationResponse(false, "Request too large.");
                        }

                        var queryDict = context.Request.Query.ToDictionary(k => k.Key, v => (object?)v.Value.ToString());
                        var bodyRequest = await context.TryReadJsonAsync();

                        //if (!bodyRequest.IsSuccess)
                        //{
                        //    if (options.DetailedErrorsEnabled)
                        //    {
                        //        await BadRequest(context);
                        //        return new BaseOperationResponse(false, bodyRequest.ErrorMessage ?? "");
                        //    }
                        //    else
                        //    {
                        //        await BadRequest(context);
                        //        return new BaseOperationResponse(false, "The request is malformed.");
                        //    }
                        //}

                        Dictionary<string, object?> args = new Dictionary<string, object?>();

                        for(int i = 0; i < orderedParameterNames.Length; i++)
                        {
                            args[orderedParameterNames[i]] = invocationContext.Arguments[i]!;
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            args
                        );

                        var res = await requestHandler.HandleWithResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            var errorMessage = options.DetailedErrorsEnabled
                                ? res.Error ?? "Internal error"
                                : "Internal error";

                            await InternalServerError(context);
                            return new BaseOperationResponse(false, errorMessage);
                        }

                        await Ok(context);
                        return res;
                    }).ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
            }
            else
            {
                if (verbResult == HttpMethod.Get)
                {
                    builder = endpointGroup.MapGet(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var requestHandler = services.GetRequiredService<IRequestHandler>();
                        var cancellationToken = context.RequestAborted;

                        // Ya no necesitamos limitar el tamaño del body
                        // var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        // mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        var operationRequest = new OperationRequest(operationName, contractName);

                        // Parsear argumentos desde query string
                        foreach (var kvp in context.Request.Query)
                        {
                            // kvp.Key = nombre del argumento
                            // kvp.Value = valor del argumento (StringValues)
                            operationRequest.Arguments[kvp.Key] = kvp.Value.ToString();
                        }

                        var res = await requestHandler.HandleWithoutResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            var errorMessage = options.DetailedErrorsEnabled
                                ? res.Error ?? "Internal error"
                                : "Internal error";

                            await InternalServerError(context);
                            return new BaseOperationResponse(false, errorMessage);
                        }

                        await Ok(context);
                        return res;
                    }).ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
                else 
                {
                    builder = endpointGroup.MapPost(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var requestHandler = services.GetRequiredService<IRequestHandler>();
                        var converter = services.GetRequiredService<IDynamicConverter>();
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            await RequestTooLarge(context);
                            return new BaseOperationResponse(false, "Request too large.");
                        }

                        Dictionary<string, object?> args = new Dictionary<string, object?>();

                        for (int i = 0; i < orderedParameterNames.Length; i++)
                        {
                            args[orderedParameterNames[i]] = invocationContext.Arguments[i]!;
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            args
                        );

                        var res = await requestHandler.HandleWithoutResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            if (options.DetailedErrorsEnabled)
                            {
                                await InternalServerError(context);
                                return new BaseOperationResponse(false, res.Error);
                            }
                            else
                            {
                                await InternalServerError(context);
                                return new BaseOperationResponse(false, "Internal error");
                            }
                        }

                        await Ok(context);
                        return res;
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
            }
        }

        static void SetupEndpointGroup(
                IInternalServerOptions options,
                RouteHandlerBuilder builder,
                RouteGroupBuilder endpointGroup,
                IOperationBlueprint blueprint,
                MethodInfo controllerMethod,
                List<UseHttpEndpointFilterAttribute>? filters)
        {
            options.EndpointConventions?.Invoke(builder);

            foreach (var filter in filters)
            {
                methodInfo.MakeGenericMethod(filter.EndpointFilterType).Invoke(null, [builder]);
            }

            if (!options.ThrottlingIsDisabled)
            {
                var limiterApplied = RateLimiterApplied.TryGetValue(endpointGroup, out var result);
                if (!limiterApplied)
                {
                    var ContractRateLimiter = blueprint.ControllerType.GetCustomAttributes<UseHttpRateLimiterAttribute>().FirstOrDefault();
                    if (ContractRateLimiter != null)
                    {
                        endpointGroup.RequireRateLimiting(ContractRateLimiter.Policy);
                        RateLimiterApplied.TryAdd(endpointGroup, true);
                    }
                }

                var OperationRateLimiter = controllerMethod!.GetCustomAttributes<UseHttpRateLimiterAttribute>().FirstOrDefault();
                if (OperationRateLimiter != null)
                    builder.RequireRateLimiting(OperationRateLimiter.Policy);
            }

            options.RouteHandlerBuilderConfig?.Invoke(builder);
        }

        private static async Task Ok(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
        }

        private static async Task BadRequest(HttpContext context)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
        }

        private static async Task InternalServerError(HttpContext context)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
        }

        private static async Task RequestTooLarge(HttpContext context)
        {
            context.Response.StatusCode = 413;
            context.Response.ContentType = "application/json";
        }

        public static Delegate CreateDelegate(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));

            var paramTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
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

            if (methodInfo.IsStatic)
            {
                return Delegate.CreateDelegate(delegateType, methodInfo);
            }
            else
            {
                object instance = FormatterServices.GetUninitializedObject(methodInfo.DeclaringType!);
                return Delegate.CreateDelegate(delegateType, instance, methodInfo);
            }
        }
    }
}
