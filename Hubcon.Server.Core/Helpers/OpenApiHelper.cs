using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace Hubcon.Server.Core.Helpers
{
    public static class OpenApiHelper
    {
        public static (OpenApiSchema Schema, IOpenApiAny Example) GenerateSchemaAndExampleFromParameters(ParameterInfo[] parameters)
        {
            var schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>(),
                Required = new HashSet<string>()
            };

            var example = new OpenApiObject();

            foreach (var param in parameters)
            {
                var (propertySchema, propertyExample) = GenerateSchemaAndExampleFromType(param.ParameterType);
                schema.Properties[param.Name!] = propertySchema;
                schema.Required.Add(param.Name!);
                example[param.Name!] = propertyExample;
            }

            return (schema, example);
        }

        private static (OpenApiSchema, IOpenApiAny) GenerateSchemaAndExampleFromType(Type type)
        {
            if (type == typeof(string))
                return (new OpenApiSchema { Type = "string" }, new OpenApiString("string"));

            if (type == typeof(int) || type == typeof(int?))
                return (new OpenApiSchema { Type = "integer", Format = "int32" }, new OpenApiInteger(123));

            if (type == typeof(long) || type == typeof(long?))
                return (new OpenApiSchema { Type = "integer", Format = "int64" }, new OpenApiLong(1234567890));

            if (type == typeof(bool) || type == typeof(bool?))
                return (new OpenApiSchema { Type = "boolean" }, new OpenApiBoolean(true));

            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                return (new OpenApiSchema { Type = "number", Format = "double" }, new OpenApiDouble(12.34));

            if (type == typeof(DateTime) || type == typeof(DateTime?))
                return (new OpenApiSchema { Type = "string", Format = "date-time" }, new OpenApiString(DateTime.UtcNow.ToString("o")));

            if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                return (
                    new OpenApiSchema { Type = "string", Enum = names.Select(n => new OpenApiString(n)).ToList<IOpenApiAny>() },
                    new OpenApiString(names.FirstOrDefault() ?? "EnumValue")
                );
            }

            if (typeof(IEnumerable<string>).IsAssignableFrom(type))
                return (
                    new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "string" } },
                    new OpenApiArray { new OpenApiString("item1"), new OpenApiString("item2") }
                );

            // Objetos complejos: recursión
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var objSchema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>(),
                Required = new HashSet<string>()
            };

            var objExample = new OpenApiObject();

            foreach (var prop in properties)
            {
                var (propSchema, propExample) = GenerateSchemaAndExampleFromType(prop.PropertyType);
                objSchema.Properties[prop.Name] = propSchema;
                objExample[prop.Name] = propExample;
            }

            return (objSchema, objExample);
        }
    }

}
