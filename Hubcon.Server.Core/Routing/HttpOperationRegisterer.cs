using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.Helpers;
using Hubcon.Server.Core.Routing.Models;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Tools;
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
            if (!blueprint.SupportsTransport<HttpTransport>())
                return;

            IEndpointConventionBuilder builder = null!;
            var route = blueprint.HttpRoute!;
            var operationName = blueprint.OperationName;
            var simpleContractName = NamingHelper.GetCleanName(blueprint.ContractName);
            var options = app.Services.GetRequiredService<IInternalServerOptions>();
            var method = (MethodInfo)blueprint.MemberInfo!;

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

            var endpointGroup = EndpointGroups.GetOrAdd(blueprint.HttpEndpointGroupName, x =>
            {
                var group = app.MapGroup(x);
                return group;
            });

            HttpMethod verbResult = blueprint.HttpVerb!;
            var wrapperType = blueprint.CallWrapperType!;
            var wrapperProps = wrapperType.GetProperties();


            if(blueprint.Kind == OperationKind.Stream)
            {
                if (verbResult == HttpMethod.Get)
                {
                    var endpointDelegate = CreateDelegate(controllerMethod!, wrapperType, true);
                    builder = endpointGroup.MapGet(route, endpointDelegate);

                    // 2. Registramos el GET con un RequestDelegate manual
                    //builder = app.MapGet(route, async (HttpContext context) => { });

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        var wrapper = Activator.CreateInstance(wrapperType);

                        // Llenamos solo lo que sea Simple Type desde la Query
                        foreach (var prop in wrapperType.GetProperties())
                        {
                            var value = context.Request.Query[prop.Name];
                            if (value.Count > 0)
                            {
                                var converted = Convert.ChangeType(value.ToString(), prop.PropertyType);
                                prop.SetValue(wrapper, converted);
                            }
                        }

                        var ctProp = wrapperType.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(CancellationToken));
                        ctProp?.SetValue(wrapper, context.RequestAborted);

                        var dict = context.Request.Query
                            .Cast<KeyValuePair<string, StringValues>>()
                            .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.ToString());

                        var operationRequest = new OperationRequest(operationName, simpleContractName, dict);
                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest, 
                            HubconTransportAttribute.GetDefault<HttpTransport>(), 
                            services, 
                            wrapper, 
                            cancellationToken);

                        if (res.Failure)
                        {
                            return ProcessResponse(res, context);
                        }

                        return new SseResult((res.Data as IAsyncEnumerable<object?>)!, operationRequest);
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                }
                else
                {
                    var endpointDelegate = CreateDelegate(controllerMethod!, wrapperType, false);
                    builder = endpointGroup.MapPost(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

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

                        var wrapper = invocationContext.Arguments.FirstOrDefault(a => a?.GetType() == wrapperType);

                        if (wrapper == null)
                        {
                            var response = HubconResponse.BadRequest();
                            return ProcessResponse(response, context);
                        }

                        var args = new Dictionary<string, object>();

                        foreach (var prop in wrapperProps)
                        {
                            var value = prop.GetValue(wrapper);
                            args[prop.Name!] = value!;
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName,
                            args
                        );

                        var res = await DefaultEntrypoint.HandleMethodStream(
                            operationRequest,
                            HubconTransportAttribute.GetDefault<HttpTransport>(),
                            services,
                            wrapper,
                            cancellationToken);

                        if (res.Failure)
                        {
                            return ProcessResponse(res, context);
                        }

                        return new SseResult((res.Data! as IAsyncEnumerable<object?>)!, operationRequest);
                    }).ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
            }
            else if (blueprint.HasReturnType)
            {
                if (verbResult == HttpMethod.Get)
                {
                    var endpointDelegate = CreateDelegate(controllerMethod!, wrapperType, true);
                    builder = endpointGroup.MapGet(route, endpointDelegate);

                    // 2. Registramos el GET con un RequestDelegate manual
                    //builder = app.MapGet(route, async (HttpContext context) => { });

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        // Creamos la instancia del Wrapper (el Monstruo)
                        var wrapper = Activator.CreateInstance(wrapperType);

                        // Llenamos solo lo que sea Simple Type desde la Query
                        foreach (var prop in wrapperType.GetProperties())
                        {
                            var value = context.Request.Query[prop.Name];
                            if (value.Count > 0)
                            {
                                // Aquí puedes usar un TypeConverter o un simple Convert.ChangeType
                                // Para performance extrema, esto se puede pre-compilar con IL
                                var converted = Convert.ChangeType(value.ToString(), prop.PropertyType);
                                prop.SetValue(wrapper, converted);
                            }
                        }

                        // Inyectamos el CancellationToken si el Wrapper lo tiene
                        // (Esto soluciona tu problema anterior también)
                        var ctProp = wrapperType.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(CancellationToken));
                        ctProp?.SetValue(wrapper, context.RequestAborted);

                        var dict = context.Request.Query
                            .Cast<KeyValuePair<string, StringValues>>()
                            .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.ToString());

                        var operationRequest = new OperationRequest(operationName, simpleContractName, dict);
                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            HubconTransportAttribute.GetDefault<HttpTransport>(),
                            services,
                            wrapper,
                            cancellationToken);

                        return res;
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                }
                else
                {
                    var endpointDelegate = CreateDelegate(controllerMethod!, wrapperType, false);
                    builder = endpointGroup.MapPost(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

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

                        var wrapper = invocationContext.Arguments.FirstOrDefault(a => a?.GetType() == wrapperType);

                        if (wrapper == null)
                        {
                            var response = HubconResponse.BadRequest();
                            return ProcessResponse(response, context);
                        }

                        var args = new Dictionary<string, object>();

                        foreach (var prop in wrapperProps)
                        {
                            var value = prop.GetValue(wrapper);
                            args[prop.Name!] = value!;
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName,
                            args
                        );

                        var res = await DefaultEntrypoint.HandleMethodWithResult(
                            operationRequest,
                            HubconTransportAttribute.GetDefault<HttpTransport>(),
                            services,
                            wrapper,
                            cancellationToken);

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
                    var endpointDelegate = CreateDelegate(controllerMethod!, wrapperType, true);
                    builder = endpointGroup.MapGet(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

                    builder.AddEndpointFilter(async (invocationContext, next) =>
                    {
                        var context = invocationContext.HttpContext;
                        var services = context.RequestServices;
                        var cancellationToken = context.RequestAborted;

                        var dict = new Dictionary<string, object>();
                        // Parsear argumentos desde query string
                        foreach (var kvp in context.Request.Query)
                        {
                            dict[kvp.Key] = kvp.Value.ToString();
                        }

                        var operationRequest = new OperationRequest(operationName, simpleContractName, dict);

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            HubconTransportAttribute.GetDefault<HttpTransport>(),
                            services,
                            null,
                            cancellationToken);

                        return res;
                    }).ApplyOpenApiFromMethod(controllerMethod!, verbResult).WithMetadata(new AsParametersAttribute());
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
                else
                {
                    var endpointDelegate = CreateDelegate(controllerMethod!, wrapperType, false);
                    builder = endpointGroup.MapPost(route, endpointDelegate);

                    SetupEndpointGroup(options, builder, endpointGroup, blueprint, controllerMethod!, filters);

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

                        var wrapper = invocationContext.Arguments.FirstOrDefault(a => a?.GetType() == wrapperType);

                        if (wrapper == null)
                        {
                            var response = HubconResponse.BadRequest();
                            return ProcessResponse(response, context);
                        }

                        var args = new Dictionary<string, object>();

                        foreach (var prop in wrapperProps)
                        {
                            var value = prop.GetValue(wrapper);
                            args[prop.Name!] = value!;
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            simpleContractName,
                            args
                        );

                        var res = await DefaultEntrypoint.HandleMethodVoid(
                            operationRequest,
                            HubconTransportAttribute.GetDefault<HttpTransport>(),
                            services,
                            null,
                            cancellationToken);

                        return res;
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!, verbResult);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
            }
        }

        public static (IResult Result, IReadOnlyDictionary<string, string[]> Errors) TriggerValidators(object wrapper)
        {
            // 2. ACTIVAR LA VALIDACIÓN
            var validationContext = new ValidationContext(wrapper);
            var validationResults = new List<ValidationResult>();

            var type = wrapper.GetType();

            // Esto disparará todos los [Required], [StringLength], etc. que clonamos
            if (!Validator.TryValidateObject(wrapper, validationContext, validationResults, true))
            {
                // Si hay errores, devolvemos un 400 Bad Request con los detalles
                var errors = validationResults.ToDictionary(
                    k => k.MemberNames.FirstOrDefault() ?? "error",
                    v => new[] { v.ErrorMessage ?? "Invalid value" }
                );
                return (Results.ValidationProblem(errors), errors);
            }

            return (Results.Ok(), ReadOnlyDictionary<string, string[]>.Empty);
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

            options.RouteHandlerBuilderConfig?.Invoke((builder as RouteHandlerBuilder)!);
        }


        private static IHubconResponse<T> ProcessResponse<T>(IHubconResponse<T> hubconResponse, HttpContext context)
        {
            context.Response.StatusCode = hubconResponse.StatusCode;
            context.Response.ContentType = "application/json";
            return hubconResponse;
        }

        public static Delegate CreateDelegate(MethodInfo methodInfo, Type wrapperType, bool isGet = false)
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
