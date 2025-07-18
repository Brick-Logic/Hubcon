using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;

namespace Hubcon.Server.Core.EndpointDocumentation
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class RemoveNullableTypesOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Procesar respuestas
            foreach (var response in operation.Responses.Values)
            {
                foreach (var content in response.Content.Values)
                {
                    if (content.Schema != null)
                    {
                        CleanSchema(content.Schema);
                    }
                }
            }
        }

        private void CleanSchema(OpenApiSchema schema)
        {
            if (schema.Properties != null)
            {
                foreach (var (propertyName, property) in schema.Properties.ToList())
                {
                    // Limpiar OneOf
                    if (property.OneOf?.Count > 0)
                    {
                        var nonNullTypes = property.OneOf.Where(x => x.Type != "null").ToList();

                        if (nonNullTypes.Count == 1)
                        {
                            var singleType = nonNullTypes.First();
                            property.Type = singleType.Type;
                            property.OneOf = null;

                            // Establecer valor por defecto
                            if (propertyName == "data")
                            {
                                property.Example = GetDefaultExample(singleType.Type);
                            }
                            else if (propertyName == "error")
                            {
                                property.Example = new OpenApiString("");
                            }
                        }
                    }
                }
            }
        }

        private IOpenApiAny GetDefaultExample(string type)
        {
            return type switch
            {
                "string" => new OpenApiString(""),
                "integer" => new OpenApiInteger(0),
                "boolean" => new OpenApiBoolean(false),
                "number" => new OpenApiDouble(0.0),
                _ => new OpenApiString("")
            };
        }
    }
}
