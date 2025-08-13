using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Helpers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hubcon.Server.Core.Routing
{
    public static class HttpOperationRegisterer
    {
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
                BindingFlags.Public | BindingFlags.Instance,
                method.GetParameters().Select(x => x.ParameterType).ToArray());

            var returnType = typeof(IOperationResponse<>).MakeGenericType(blueprint.ReturnType);

            if (blueprint.HasReturnType)
            {
                if(blueprint.ParameterTypes.Count - blueprint.ParameterTypes.Count(x => x.Value == typeof(CancellationToken)) > 0)
                {
                    builder = app.MapPost(route, async (HttpContext context, IRequestHandler requestHandler, IDynamicConverter converter, CancellationToken cancellationToken) =>
                    {
                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        context.Request.EnableBuffering();

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            await RequestTooLarge(context, new BaseOperationResponse(false, "Request too large."));
                            return;
                        }

                        var request = await context.TryReadJsonAsync();

                        if (!request.IsSuccess)
                        {
                            if (options.DetailedErrorsEnabled)
                            {
                                await BadRequest(context, new BaseOperationResponse(false, request.ErrorMessage ?? ""));
                                return;
                            }
                            else
                            {
                                await BadRequest(context, new BaseOperationResponse(false, "The request is malformed."));
                                return;
                            }
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            converter.DeserializeData<Dictionary<string, object?>>(request.JsonElement)
                        );

                        var res = await requestHandler.HandleWithResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            await InternalServerError(context, res);
                            return;
                        }

                        await Ok(context, res);
                        return;
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
                else
                {
                    builder = app.MapGet(route, async (IRequestHandler requestHandler, HttpContext context, CancellationToken cancellationToken) =>
                    {
                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        context.Request.EnableBuffering();

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            await RequestTooLarge(context, new BaseOperationResponse(false, "Request too large."));
                            return;
                        }

                        var operationRequest = new OperationRequest(operationName, contractName);
                        var res = await requestHandler.HandleWithResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            await InternalServerError(context, res);
                            return;
                        }

                        await Ok(context, res);
                        return;
                    }).ApplyOpenApiFromMethod(controllerMethod!);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
            }
            else
            {
                if(blueprint.ParameterTypes.Count - blueprint.ParameterTypes.Count(x => x.Value == typeof(CancellationToken)) > 0)
                {
                    builder = app.MapPost(route, async (HttpContext context, IRequestHandler requestHandler, IDynamicConverter converter, CancellationToken cancellationToken) =>
                    {
                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        context.Request.EnableBuffering();

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            await RequestTooLarge(context, new BaseOperationResponse(false, "Request too large."));
                            return;
                        }

                        var request = await context.TryReadJsonAsync();

                        if (!request.IsSuccess)
                        {
                            if (options.DetailedErrorsEnabled)
                            {
                                await BadRequest(context, new BaseOperationResponse(false, request.ErrorMessage ?? ""));
                                return;
                            }
                            else
                            {
                                await BadRequest(context, new BaseOperationResponse(false, "Request invalido."));
                                return;
                            }
                        }

                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            converter.DeserializeData<Dictionary<string, object?>>(request.JsonElement)
                        );

                        var res = await requestHandler.HandleWithoutResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            if (options.DetailedErrorsEnabled)
                            {
                                await InternalServerError(context, new BaseOperationResponse(false, request.ErrorMessage ?? "Internal error"));
                                return;
                            }
                            else
                            {
                                await InternalServerError(context, new BaseOperationResponse(false, "Internal error"));
                                return;
                            }
                        }

                        await Ok(context, res);
                        return;
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }
                else
                {
                    builder = app.MapGet(route, async (IRequestHandler requestHandler, HttpContext context, CancellationToken cancellationToken) =>
                    {
                        var mrbs = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!;
                        mrbs.MaxRequestBodySize = options.MaxHttpMessageSize;

                        context.Request.EnableBuffering();

                        if (context.Request.ContentLength > options.MaxHttpMessageSize)
                        {
                            await RequestTooLarge(context, new BaseOperationResponse(false, "Request too large."));
                            return;
                        }

                        var operationRequest = new OperationRequest(operationName, contractName);
                        var res = await requestHandler.HandleWithoutResultAsync(operationRequest, cancellationToken);

                        if (!res.Success)
                        {
                            if (options.DetailedErrorsEnabled)
                            {
                                await InternalServerError(context, new BaseOperationResponse(false, res.Error ?? "Internal error"));
                                return;
                            }
                            else
                            {
                                await InternalServerError(context, new BaseOperationResponse(false, "Internal error"));
                                return;
                            }
                        }

                        await Ok(context, res);
                        return;
                    }).ApplyOpenApiFromMethod(controllerMethod!);
                    builder.WithRequestTimeout(options.HttpTimeout);
                    options.EndpointConventions?.Invoke(builder);
                }

                options.RouteHandlerBuilderConfig?.Invoke(builder);
            }
        }
        private static async Task Ok<T>(HttpContext context, T response)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }

        private static async Task BadRequest(HttpContext context, IResponse response)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }

        private static async Task InternalServerError(HttpContext context, IResponse response)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }

        private static async Task RequestTooLarge(HttpContext context, IResponse response)
        {
            context.Response.StatusCode = 413;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
