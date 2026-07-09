using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;
using System.Text.Json;
#pragma warning disable CS1591
namespace Hubcon.Server.Core.Extensions;
//
// public static class OpenApiExtensions
// {
//     //public static RouteHandlerBuilder MapRpcEndpoint(this RouteGroupBuilder group, string route, MethodInfo methodInfo)
//     //{
//     //    // 1. Registro del endpoint que recibe el JSON bruto (JsonElement)
//     //    var builder = group.MapPost(route, async (JsonElement body, HttpContext context) =>
//     //    {
//     //        // Aquí invocarías tu mecanismo de parsing y ejecución:
//     //        // var result = await YourDispatcher.Execute(methodInfo, body, context);
//     //        return Results.Ok(new { message = "Ejecutado", method = methodInfo.Name });
//     //    });
//
//     //    // 2. Configuración dinámica de OpenAPI
//     //    builder.WithOpenApi(operation =>
//     //    {
//     //        operation.Summary = $"RPC: {methodInfo.DeclaringType?.Name}.{methodInfo.Name}";
//
//     //        // Construimos el esquema del objeto que envuelve todos los parámetros
//     //        var rootSchema = new OpenApiSchema
//     //        {
//     //            Type = "object",
//     //            Properties = methodInfo.GetParameters().ToDictionary(
//     //                p => p.Name ?? "arg",
//     //                p => MapTypeToOpenApiSchema(p.ParameterType)
//     //            )
//     //        };
//
//     //        operation.RequestBody = new OpenApiRequestBody
//     //        {
//     //            Description = "Cuerpo de la solicitud RPC",
//     //            Required = true,
//     //            Content =
//     //        {
//     //            ["application/json"] = new OpenApiMediaType { Schema = rootSchema }
//     //        }
//     //        };
//
//     //        return operation;
//     //    });
//
//     //    return builder;
//     //}
//
//     private static OpenApiSchema MapTypeToOpenApiSchema(Type type)
//     {
//         var actualType = Nullable.GetUnderlyingType(type) ?? type;
//
//         // Numéricos
//         if (actualType == typeof(int) || actualType == typeof(short))
//             return new OpenApiSchema { Type = "integer", Format = "int32" };
//         if (actualType == typeof(long))
//             return new OpenApiSchema { Type = "integer", Format = "int64" };
//         if (actualType == typeof(float) || actualType == typeof(double) || actualType == typeof(decimal))
//             return new OpenApiSchema { Type = "number", Format = "double" };
//
//         // Booleanos y Strings
//         if (actualType == typeof(bool))
//             return new OpenApiSchema { Type = "boolean" };
//         if (actualType == typeof(string) || actualType == typeof(Guid))
//             return new OpenApiSchema { Type = "string", Format = actualType == typeof(Guid) ? "uuid" : null };
//
//         // Fechas
//         if (actualType == typeof(DateTime) || actualType == typeof(DateTimeOffset))
//             return new OpenApiSchema { Type = "string", Format = "date-time" };
//
//         // Enums (Uso de OpenApiString para evitar errores de interfaz)
//         if (actualType.IsEnum)
//         {
//             var enumSchema = new OpenApiSchema { Type = "string" };
//             foreach (var name in Enum.GetNames(actualType))
//                 enumSchema.Enum.Add(new OpenApiString(name));
//             return enumSchema;
//         }
//
//         // Listas / Colecciones
//         if (actualType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(actualType))
//         {
//             var itemType = actualType.IsArray
//                 ? actualType.GetElementType()
//                 : actualType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
//
//             return new OpenApiSchema
//             {
//                 Type = "array",
//                 Items = MapTypeToOpenApiSchema(itemType!)
//             };
//         }
//
//         // Objetos Complejos (Records o Clases de usuario)
//         if (!actualType.IsPrimitive && actualType.Namespace != null && !actualType.Namespace.StartsWith("System"))
//         {
//             return new OpenApiSchema
//             {
//                 Type = "object",
//                 Properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
//                     .ToDictionary(
//                         p => p.Name,
//                         p => MapTypeToOpenApiSchema(p.PropertyType)
//                     )
//             };
//         }
//
//         return new OpenApiSchema { Type = "object" };
//     }
// }
