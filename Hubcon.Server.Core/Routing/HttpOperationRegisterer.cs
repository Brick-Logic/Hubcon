using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Helpers;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Entrypoint;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hubcon.Shared.Abstractions.Interfaces;

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

            if(blueprint.HasReturnType)
            {
                if(blueprint.ParameterTypes.Length > 0)
                {
                    builder = app.MapPost(route, ([FromBody] JsonElement request, DefaultEntrypoint entrypoint, IDynamicConverter converter) =>
                    {
                        var operationRequest = new OperationRequest(
                            operationName, 
                            contractName, 
                            converter.DeserializeJsonElement<JsonElement[]>(request)
                        );

                        var res = entrypoint.HandleMethodWithResult(operationRequest);
                        return Results.Ok(res);
                    });
                }
                else
                {
                    builder = app.MapGet(route, (DefaultEntrypoint entrypoint) =>
                    {
                        var operationRequest = new OperationRequest(operationName, contractName);
                        var res = entrypoint.HandleMethodWithResult(operationRequest);
                        return Results.Ok(res);
                    });
                }
            }
            else
            {
                if(blueprint.ParameterTypes.Length > 0)
                {
                    builder = app.MapPost(route, ([FromBody] JsonElement request, DefaultEntrypoint entrypoint, IDynamicConverter converter) =>
                    {
                        var operationRequest = new OperationRequest(
                            operationName,
                            contractName,
                            converter.DeserializeJsonElement<JsonElement[]>(request)
                        );

                        var res = entrypoint.HandleMethodVoid(operationRequest);
                        return Results.Ok(res);
                    });
                }
                else
                {
                    builder = app.MapGet(route, (DefaultEntrypoint entrypoint) =>
                    {
                        var operationRequest = new OperationRequest(operationName, contractName);
                        var res = entrypoint.HandleMethodVoid(operationRequest);
                        return Results.Ok(res);
                    });
                }
            }

            //builder.WithName(operationName);

            //if(blueprint.ParameterTypes.Length > 0)
            //{
            //    var methodInfo = (MethodInfo)blueprint.OperationInfo!;
            //    var parameters = methodInfo.GetParameters();

            //    builder.WithOpenApi(op =>
            //    {
            //        var (schema, example) = OpenApiHelper.GenerateSchemaAndExampleFromParameters(parameters);

            //        op.RequestBody = new OpenApiRequestBody
            //        {
            //            Content =
            //            {
            //                ["application/json"] = new OpenApiMediaType
            //                {
            //                    Schema = schema,
            //                    Example = example
            //                }
            //            },
            //            Required = true
            //        };

            //        return op;
            //    });
            //}
        }

        public static IEnumerable<JsonElement> GetJsonFieldsAsEnumerable(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("El JsonElement debe ser un objeto JSON");

            foreach (var property in json.EnumerateObject())
            {
                yield return property.Value;
            }
        }
        private static Type UnwrapTask(Type t)
            => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>)
                ? t.GetGenericArguments()[0]
                : t;
    }
}
