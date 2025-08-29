using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace Hubcon.Server.Core.Helpers
{

    public static class OpenApiHelper
    {
        // Configuración por defecto
        public static class Defaults
        {
            public static string[] DefaultContentTypes { get; set; } = { "application/json" };
            public static int DefaultSuccessStatusCode { get; set; } = 200;
            public static int DefaultErrorStatusCode { get; set; } = 400;
            public static bool AddCommonErrorResponses { get; set; } = true;
            public static string DefaultGroupName { get; set; } = "API";
            public static bool EnableAutoFallbacks { get; set; } = true;

            public static List<Type> ExcludedTypes = [
            
                typeof(HttpContext),
                typeof(HttpRequest),
                typeof(HttpResponse),
                typeof(CancellationToken),
                typeof(IServiceProvider)
            ];
        }

        public static RouteHandlerBuilder ApplyOpenApiFromMethod(
            this RouteHandlerBuilder builder,
            MethodInfo methodInfo,
            string? groupName = null)
        {
            // Aplicar agrupamiento por namespace o clase
            var effectiveGroupName = groupName ?? GetGroupFromMethod(methodInfo);

            // EndpointName - con fallback automático
            var endpointName = methodInfo.GetCustomAttributes<EndpointNameAttribute>().FirstOrDefault();
            if (endpointName != null)
                builder.WithName(endpointName.EndpointName);
            //else if (Defaults.EnableAutoFallbacks)
            //    builder.WithName(GenerateDefaultEndpointName(methodInfo));

            // Summary - con fallback automático
            var summary = methodInfo.GetCustomAttributes<EndpointSummaryAttribute>().FirstOrDefault();
            if (summary != null)
                builder.WithSummary(summary.Summary);
            //else if (Defaults.EnableAutoFallbacks)
            //    builder.WithSummary(GenerateDefaultSummary(methodInfo));

            // Description
            var description = methodInfo.GetCustomAttributes<EndpointDescriptionAttribute>().FirstOrDefault();
            if (description != null)
                builder.WithDescription(description.Description);

            // Tags - usar agrupamiento si no hay tags explícitos
            var tags = methodInfo.GetCustomAttributes<TagsAttribute>().FirstOrDefault();
            if (tags != null)
                builder.WithTags(tags.Tags.ToArray());
            else if (!string.IsNullOrEmpty(effectiveGroupName))
                builder.WithTags(effectiveGroupName);

            // Produces - con valores por defecto inteligentes
            ApplyProducesWithDefaults(builder, methodInfo);

            // Accepts - con detección automática mejorada (mantener lógica original)
            ApplyAcceptsWithDefaults(builder, methodInfo);

            return builder;
        }

        public static string GetGroupFromMethod(MethodInfo methodInfo)
        {
            // 1. Primero buscar ApiExplorerSettings
            var apiExplorer = methodInfo.DeclaringType?.GetCustomAttributes<ApiExplorerSettingsAttribute>().FirstOrDefault();
            if (!string.IsNullOrEmpty(apiExplorer?.GroupName))
                return apiExplorer.GroupName;

            // 3. Por nombre de clase
            var className = methodInfo.DeclaringType?.Name ?? null;          
            RemoveInterfacePrefix(className!);

            return className ?? Defaults.DefaultGroupName;
        }


        /// <summary>
        /// Remueve la 'I' inicial si la segunda letra es mayúscula (patrón de interfaz)
        /// </summary>
        /// <param name="name">Nombre que puede tener prefijo de interfaz</param>
        /// <returns>Nombre sin el prefijo 'I' si aplica</returns>
        public static string RemoveInterfacePrefix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Verificar si empieza con 'I' y tiene al menos 2 caracteres
            if (name.Length >= 2 &&
                name[0] == 'I' &&
                char.IsUpper(name[1]))
            {
                return name.Substring(1);
            }

            return name;
        }

        /// <summary>
        /// Versión que también maneja múltiples nombres separados por comas
        /// </summary>
        /// <param name="names">Nombres separados por comas</param>
        /// <returns>Nombres procesados separados por comas</returns>
        public static string RemoveInterfacePrefixFromList(string names)
        {
            if (string.IsNullOrEmpty(names))
                return names;

            return string.Join(", ",
                names.Split(',')
                        .Select(name => RemoveInterfacePrefix(name.Trim())));
        }


        private static string GenerateDefaultEndpointName(MethodInfo methodInfo)
        {
            var className = methodInfo.DeclaringType?.Name?.Replace("Controller", "") ?? "Api";
            return $"{className}_{methodInfo.Name}";
        }

        private static string GenerateDefaultSummary(MethodInfo methodInfo)
        {
            var action = methodInfo.Name;
            var entity = methodInfo.DeclaringType?.Name?.Replace("Controller", "") ?? "Resource";

            // Patrones comunes
            return action.ToLower() switch
            {
                var name when name.StartsWith("get") => $"Obtener {entity.ToLower()}",
                var name when name.StartsWith("create") || name.StartsWith("post") => $"Crear {entity.ToLower()}",
                var name when name.StartsWith("update") || name.StartsWith("put") => $"Actualizar {entity.ToLower()}",
                var name when name.StartsWith("delete") => $"Eliminar {entity.ToLower()}",
                var name when name.StartsWith("list") => $"Listar {entity.ToLower()}",
                _ => $"{action} {entity.ToLower()}"
            };
        }

        private static void ApplyProducesWithDefaults(RouteHandlerBuilder builder, MethodInfo methodInfo)
        {
            var produces = methodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();

            // Si hay atributos explícitos, usarlos (lógica original)
            if (produces.Any())
            {
                foreach (var produce in produces)
                {
                    if (produce.Type != null && produce.Type != typeof(void))
                    {
                        builder.Produces(produce.StatusCode, produce.Type);
                    }
                    else
                    {
                        builder.Produces(produce.StatusCode);
                    }
                }
            }
            else if (Defaults.EnableAutoFallbacks)
            {
                // Aplicar valores por defecto inteligentes
                ApplyDefaultProduces(builder, methodInfo);
            }

            // Agregar respuestas de error comunes si está habilitado
            if (Defaults.AddCommonErrorResponses && !produces.Any(p => p.StatusCode >= 400))
            {
                builder.Produces(400, typeof(IResponse)); // Bad Request
                builder.Produces(500, typeof(IResponse)); // Internal Server Error
            }
        }

        private static void ApplyDefaultProduces(RouteHandlerBuilder builder, MethodInfo methodInfo)
        {
            var returnType = GetActualReturnType(methodInfo);

            if (returnType == typeof(void) || returnType == typeof(Task) || returnType == typeof(ValueTask))
            {
                builder.Produces(200, typeof(IResponse));
            }
            else if (returnType == typeof(IResult) || returnType.IsAssignableTo(typeof(IResult)))
            {
                builder.Produces(Defaults.DefaultSuccessStatusCode);
            }
            else
            {
                // Tipo específico
                var responseType = typeof(IOperationResponse<>).MakeGenericType(returnType);
                builder.Produces(Defaults.DefaultSuccessStatusCode, responseType)
                       .WithOpenApi(operation => SetDefaultExample(operation, Defaults.DefaultSuccessStatusCode.ToString(), returnType));
            }
        }

        private static OpenApiOperation SetDefaultExample(OpenApiOperation operation, string statusCode, Type dataType)
        {
            if (operation.Responses.TryGetValue(statusCode, out var response) &&
                response.Content.TryGetValue("application/json", out var content))
            {
                content.Example = CreateExampleResponse(dataType);
            }

            return operation;
        }

        private static IOpenApiAny CreateExampleResponse(Type dataType)
        {
            var defaultDataValue = GetDefaultValueForType(dataType);

            return new OpenApiObject
            {
                ["data"] = defaultDataValue,
                ["success"] = new OpenApiBoolean(true),
                ["message"] = new OpenApiString("")
            };
        }

        private static IOpenApiAny GetDefaultValueForType(Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Int32 => new OpenApiInteger(0),
                TypeCode.Int64 => new OpenApiLong(0),
                TypeCode.String => new OpenApiString("string"),
                TypeCode.Boolean => new OpenApiBoolean(false),
                TypeCode.Double => new OpenApiDouble(0.0),
                TypeCode.Decimal => new OpenApiDouble(0.0),
                TypeCode.DateTime => new OpenApiString(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                _ when type == typeof(Guid) => new OpenApiString(Guid.Empty.ToString()),
                _ when type.IsEnum => new OpenApiString(Enum.GetNames(type).FirstOrDefault() ?? "0"),
                _ when type.IsClass && type != typeof(string) => new OpenApiObject(),
                _ when type.IsArray => new OpenApiArray(),
                _ when type == typeof(CancellationToken) => new OpenApiNull(),
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) => new OpenApiArray(),
                _ => new OpenApiNull()
            };
        }

        private static void ApplyAcceptsWithDefaults(RouteHandlerBuilder builder, MethodInfo methodInfo)
        {
            // Consumes from ConsumesAttribute (lógica original)
            var consumes = methodInfo.GetCustomAttributes<ConsumesAttribute>().FirstOrDefault();
            var parameters = methodInfo.GetParameters().Where(x => !Defaults.ExcludedTypes.Any(t => t.IsAssignableFrom(x.ParameterType)));

            if (consumes != null && consumes.ContentTypes.Count > 0)
            {
                // Buscar el primer parámetro complejo para Accepts
                var complexParam = parameters
                    .FirstOrDefault(p => !p.ParameterType.IsPrimitive &&
                                   p.ParameterType != typeof(string) &&
                                   !typeof(HttpContext).IsAssignableFrom(p.ParameterType));

                if (complexParam != null)
                {
                    builder.Accepts(complexParam.ParameterType, consumes.ContentTypes[0]);
                }
            }
            else if (parameters.Any())
            {
                // Lógica original con TypeHelper
                var name = methodInfo.ReflectedType!.Name
                    + methodInfo.Name
                    + string.Join("", parameters.Select(x => StringHelper.ToPascalCase(x.Name!)))
                    + "Input";

                builder.Accepts(TypeHelper.CreateTypeFromParameters(parameters.ToArray(), name), "application/json");
            }
            else if (Defaults.EnableAutoFallbacks)
            {
                // Fallback mejorado usando detección automática
                var requestParam = FindRequestBodyParameter(methodInfo);
                if (requestParam != null)
                {
                    var contentTypes = Defaults.DefaultContentTypes;
                    builder.Accepts(requestParam.ParameterType, contentTypes[0]);
                }
            }
        }

        private static ParameterInfo? FindRequestBodyParameter(MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters().Where(x => x.ParameterType != typeof(CancellationToken));

            // Excluir tipos que no son request body

            return parameters.FirstOrDefault(p =>
                !p.ParameterType.IsPrimitive &&
                p.ParameterType != typeof(string) &&
                p.ParameterType != typeof(Guid) &&
                p.ParameterType != typeof(DateTime) &&
                !Defaults.ExcludedTypes.Any(t => t.IsAssignableFrom(p.ParameterType)) &&
                !p.ParameterType.IsEnum &&
                // Buscar atributo FromBody o asumir si es tipo complejo
                (p.GetCustomAttributes<FromBodyAttribute>().FirstOrDefault() != null ||
                 (!p.ParameterType.IsValueType && p.ParameterType.IsClass)));
        }

        private static Type GetActualReturnType(MethodInfo methodInfo)
        {
            var returnType = methodInfo.ReturnType;

            // Desenvolver Task<T> y ValueTask<T>
            if (returnType.IsGenericType)
            {
                var genericType = returnType.GetGenericTypeDefinition();
                if (genericType == typeof(Task<>) || genericType == typeof(ValueTask<>))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }
            }

            return returnType;
        }
    }
}
