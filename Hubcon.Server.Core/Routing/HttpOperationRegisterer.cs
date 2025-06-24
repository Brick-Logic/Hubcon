using Hubcon.Server.Abstractions.Interfaces;
//using Hubcon.Server.Core.Helpers;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Entrypoint;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
//using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.OpenApi.Models;
using Hubcon.Server.Core.Helpers;
using Microsoft.OpenApi.Any;
using System.Dynamic;
using Hubcon.Server.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            RouteHandlerBuilder builder = null!;
            var method = (MethodInfo)blueprint.OperationInfo!;

            var controllerMethod = blueprint.ControllerType.GetMethod(
                method.Name, 
                BindingFlags.Public | BindingFlags.Instance,
                method.GetParameters()
                .Select(x => x.ParameterType)
                .ToArray());

            var returnType = typeof(IOperationResponse<>).MakeGenericType(blueprint.ReturnType);

            if (blueprint.HasReturnType)
            {
                if(blueprint.ParameterTypes.Count > 0)
                {
                    builder = app.MapPost(route, async (HttpContext context, IRequestHandler requestHandler, IDynamicConverter converter) =>
                    {
                        var request = await ReadBodyAsJsonElementAsync(context);

                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            converter.DeserializeData<Dictionary<string, object?>>(request)
                        );

                        var res = await requestHandler.HandleWithResultAsync(operationRequest);
                        return Results.Ok(res);
                    })
                    .ApplyOpenApiFromMethod(controllerMethod!);
                }
                else
                {
                    builder = app.MapGet(route, async (IRequestHandler requestHandler, HttpContext context) =>
                    {
                        var operationRequest = new OperationRequest(operationName, contractName);
                        var res = await requestHandler.HandleWithResultAsync(operationRequest);
                        return Results.Ok(res);
                    }).ApplyOpenApiFromMethod(controllerMethod!);
                }
            }
            else
            {
                if(blueprint.ParameterTypes.Count > 0)
                {
                    builder = app.MapPost(route, async (HttpContext context, IRequestHandler requestHandler, IDynamicConverter converter) =>
                    {
                        var request = await ReadBodyAsJsonElementAsync(context);

                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            converter.DeserializeData<Dictionary<string, object?>>(request)
                        );

                        var res = await requestHandler.HandleWithoutResultAsync(operationRequest);
                        return Results.Ok(res);
                    }).ApplyOpenApiFromMethod(controllerMethod!);
                }
                else
                {
                    builder = app.MapGet(route, async (IRequestHandler requestHandler, HttpContext context) =>
                    {
                        var operationRequest = new OperationRequest(operationName, contractName);
                        var res = await requestHandler.HandleWithoutResultAsync(operationRequest);
                        return Results.Ok(res);
                    }).ApplyOpenApiFromMethod(controllerMethod!); ;
                }
            }
        }
      
        public static async Task<JsonElement> ReadBodyAsJsonElementAsync(HttpContext context)
        {
            context.Request.EnableBuffering(); // Permite releer el body
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reinicia el stream para futuras lecturas

            using var doc = JsonDocument.Parse(bodyText);
            return doc.RootElement.Clone(); // ¡Importante! Clonar porque `JsonDocument` se va a liberar
        }
    }
}
