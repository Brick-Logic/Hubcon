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

            var returnType = typeof(IOperationResponse<>).MakeGenericType(blueprint.RawReturnType);

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

        public static OpenApiRequestBody Build()
        {
            return new OpenApiRequestBody
            {
                Description = "Datos del usuario a crear. Todos los campos marcados como required son obligatorios.",
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["name"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Description = "Nombre completo del usuario",
                                    Example = new Microsoft.OpenApi.Any.OpenApiString("Juan Pérez"),
                                    MinLength = 2,
                                    MaxLength = 100
                                },
                                ["email"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "email",
                                    Description = "Dirección de correo electrónico válida",
                                    Example = new Microsoft.OpenApi.Any.OpenApiString("juan@ejemplo.com")
                                },
                                ["age"] = new OpenApiSchema
                                {
                                    Type = "integer",
                                    Format = "int32",
                                    Description = "Edad del usuario en años",
                                    Example = new Microsoft.OpenApi.Any.OpenApiInteger(25),
                                    Minimum = 0,
                                    Maximum = 150
                                },
                                ["hobbies"] = new OpenApiSchema
                                {
                                    Type = "array",
                                    Description = "Lista de hobbies o intereses del usuario",
                                    Items = new OpenApiSchema
                                    {
                                        Type = "string"
                                    },
                                    Example = new Microsoft.OpenApi.Any.OpenApiArray
                            {
                                new Microsoft.OpenApi.Any.OpenApiString("programación"),
                                new Microsoft.OpenApi.Any.OpenApiString("lectura"),
                                new Microsoft.OpenApi.Any.OpenApiString("deportes")
                            }
                                }
                            },
                            Required = new HashSet<string> { "name", "email" }
                        },
                        Examples = new Dictionary<string, OpenApiExample>
                        {
                            ["ejemplo1"] = new OpenApiExample
                            {
                                Summary = "Usuario básico",
                                Description = "Ejemplo con datos mínimos requeridos",
                                Value = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["name"] = new Microsoft.OpenApi.Any.OpenApiString("Ana García"),
                                    ["email"] = new Microsoft.OpenApi.Any.OpenApiString("ana@ejemplo.com"),
                                    ["age"] = new Microsoft.OpenApi.Any.OpenApiInteger(30),
                                    ["hobbies"] = new Microsoft.OpenApi.Any.OpenApiArray()
                                }
                            },
                            ["ejemplo2"] = new OpenApiExample
                            {
                                Summary = "Usuario completo",
                                Description = "Ejemplo con todos los campos poblados",
                                Value = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["name"] = new Microsoft.OpenApi.Any.OpenApiString("Carlos López"),
                                    ["email"] = new Microsoft.OpenApi.Any.OpenApiString("carlos@ejemplo.com"),
                                    ["age"] = new Microsoft.OpenApi.Any.OpenApiInteger(28),
                                    ["hobbies"] = new Microsoft.OpenApi.Any.OpenApiArray
                            {
                                new Microsoft.OpenApi.Any.OpenApiString("fotografía"),
                                new Microsoft.OpenApi.Any.OpenApiString("cocina"),
                                new Microsoft.OpenApi.Any.OpenApiString("viajes")
                            }
                                }
                            }
                        }
                    }
                }
            };
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


        public static OpenApiRequestBody BuildRequestBodyFromMethod(MethodInfo method)
        {
            var example = new OpenApiObject();

            foreach (var param in method.GetParameters())
            {
                var type = param.ParameterType;
                IOpenApiAny value = type switch
                {
                    Type t when t == typeof(string) => new OpenApiString("string"),
                    Type t when t == typeof(int) => new OpenApiInteger(123),
                    Type t when t == typeof(bool) => new OpenApiBoolean(true),
                    Type t when t == typeof(double) || t == typeof(float) => new OpenApiDouble(1.23),
                    Type t when t.IsEnum => new OpenApiString(Enum.GetNames(t).FirstOrDefault() ?? "value"),
                    _ => new OpenApiString($"<{type.Name}>")
                };

                example[param.Name!] = value;
            }

            return new OpenApiRequestBody
            {
                Content =
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Example = example
                    }
                }
            };
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
