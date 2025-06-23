using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;


namespace Hubcon.Server.Core.Helpers
{
    public static class TypeHelper
    {
        private static readonly ModuleBuilder _moduleBuilder;

        static TypeHelper()
        {
            var assemblyName = new AssemblyName("DynamicTypes");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                assemblyName, AssemblyBuilderAccess.Run);
            _moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
        }

        public static Type CreateTypeFromParameters(ParameterInfo[] parameters, string? typeName = null)
        {
            // Generar nombre único si no se proporciona
            var name = typeName ?? $"DynamicType_{Guid.NewGuid():N}";
            var typeBuilder = _moduleBuilder.DefineType(
                name,
                TypeAttributes.Public | TypeAttributes.Class);

            // Crear constructor por defecto
            CreateDefaultConstructor(typeBuilder);

            // Crear propiedades basadas en los parámetros
            foreach (var parameter in parameters)
            {
                CreatePropertyFromParameter(typeBuilder, parameter);
            }

            return typeBuilder.CreateType()!;
        }

        private static void CreateDefaultConstructor(TypeBuilder typeBuilder)
        {
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);

            var il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            il.Emit(OpCodes.Ret);
        }

        private static void CreatePropertyFromParameter(TypeBuilder typeBuilder, ParameterInfo parameter)
        {
            var propertyName = StringHelper.ToPascalCase(parameter.Name ?? "Property");
            var propertyType = parameter.ParameterType;

            // Campo privado
            var fieldBuilder = typeBuilder.DefineField(
                $"_{propertyName.ToLower()}",
                propertyType,
                FieldAttributes.Private);

            // Propiedad pública
            var propertyBuilder = typeBuilder.DefineProperty(
                propertyName,
                PropertyAttributes.HasDefault,
                propertyType,
                null);

            // *** AGREGAR ATRIBUTOS DE EJEMPLO ***
            AddExampleAttributes(propertyBuilder, parameter.Name!, propertyType);

            // Método Get
            var getMethod = typeBuilder.DefineMethod(
                $"get_{propertyName}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType,
                Type.EmptyTypes);

            var getIL = getMethod.GetILGenerator();
            getIL.Emit(OpCodes.Ldarg_0);
            getIL.Emit(OpCodes.Ldfld, fieldBuilder);
            getIL.Emit(OpCodes.Ret);

            // Método Set
            var setMethod = typeBuilder.DefineMethod(
                $"set_{propertyName}",
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                new[] { propertyType });

            var setIL = setMethod.GetILGenerator();
            setIL.Emit(OpCodes.Ldarg_0);
            setIL.Emit(OpCodes.Ldarg_1);
            setIL.Emit(OpCodes.Stfld, fieldBuilder);
            setIL.Emit(OpCodes.Ret);

            // Asignar métodos a la propiedad
            propertyBuilder.SetGetMethod(getMethod);
            propertyBuilder.SetSetMethod(setMethod);
        }

        private static void AddExampleAttributes(PropertyBuilder propertyBuilder, string parameterName, Type propertyType)
        {
            var exampleValue = GetExampleValue(parameterName, propertyType);

            if (exampleValue != null)
            {
                // Opción 1: DefaultValue attribute (más compatible)
                var defaultValueAttr = new CustomAttributeBuilder(
                    typeof(DefaultValueAttribute).GetConstructor(new[] { typeof(object) })!,
                    new[] { exampleValue }
                );
                propertyBuilder.SetCustomAttribute(defaultValueAttr);

                // Opción 2: Description attribute con ejemplo
                var description = $"Example: {exampleValue}";
                var descriptionAttr = new CustomAttributeBuilder(
                    typeof(DescriptionAttribute).GetConstructor(new[] { typeof(string) })!,
                    new[] { description }
                );
                propertyBuilder.SetCustomAttribute(descriptionAttr);

                // Opción 3: Si tienes Swashbuckle, puedes usar SwaggerSchema
                // var swaggerAttr = new CustomAttributeBuilder(
                //     typeof(SwaggerSchemaAttribute).GetConstructor(Type.EmptyTypes)!,
                //     new object[0],
                //     new[] { typeof(SwaggerSchemaAttribute).GetProperty("Example")! },
                //     new[] { exampleValue }
                // );
                // propertyBuilder.SetCustomAttribute(swaggerAttr);
            }
        }

        private static object? GetExampleValue(string parameterName, Type propertyType)
        {
            var lowerName = parameterName.ToLower();

            // Ejemplos específicos por nombre
            var stringExamples = lowerName switch
            {
                "username" or "user" or "login" => "johndoe",
                "password" or "pass" or "pwd" => "password123",
                "email" or "mail" or "emailaddress" => "user@example.com",
                "name" or "fullname" => "John Doe",
                "firstname" or "givenname" => "John",
                "lastname" or "surname" or "familyname" => "Doe",
                "phone" or "mobile" or "phonenumber" => "+1234567890",
                "address" or "streetaddress" => "123 Main Street",
                "city" or "town" => "New York",
                "state" or "province" => "NY",
                "country" or "nation" => "USA",
                "zipcode" or "postalcode" => "10001",
                "description" or "desc" => "Sample description",
                "title" or "subject" => "Sample Title",
                "content" or "body" or "message" => "Sample content here",
                "url" or "website" or "link" => "https://example.com",
                "token" or "accesstoken" => "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                "status" or "state" => "active",
                "role" or "usertype" => "user",
                "category" or "type" => "general",
                "tag" or "label" => "important",
                "code" or "identifier" => "ABC123",
                "reference" or "ref" => "REF-001",
                _ => GetGenericExample(lowerName, propertyType)
            };

            // Convertir según el tipo
            return propertyType switch
            {
                Type t when t == typeof(string) => stringExamples,
                Type t when t == typeof(int) => GetIntExample(lowerName),
                Type t when t == typeof(long) => GetLongExample(lowerName),
                Type t when t == typeof(decimal) => GetDecimalExample(lowerName),
                Type t when t == typeof(double) => GetDoubleExample(lowerName),
                Type t when t == typeof(float) => GetFloatExample(lowerName),
                Type t when t == typeof(bool) => GetBoolExample(lowerName),
                Type t when t == typeof(DateTime) => GetDateTimeExample(lowerName),
                Type t when t == typeof(Guid) => Guid.Parse("12345678-1234-5678-9012-123456789012"),
                Type t when t.IsEnum => GetEnumExample(t),
                // Tipos nullable
                Type t when Nullable.GetUnderlyingType(t) != null => GetExampleValue(parameterName, Nullable.GetUnderlyingType(t)!),
                _ => null
            };
        }

        private static string GetGenericExample(string paramName, Type propertyType)
        {
            return propertyType == typeof(string) ? "example" : "sample";
        }

        private static int GetIntExample(string paramName)
        {
            return paramName switch
            {
                var name when name.Contains("id") => 123,
                var name when name.Contains("age") => 25,
                var name when name.Contains("count") || name.Contains("quantity") => 5,
                var name when name.Contains("page") => 1,
                var name when name.Contains("size") || name.Contains("limit") => 10,
                var name when name.Contains("year") => DateTime.Now.Year,
                var name when name.Contains("month") => DateTime.Now.Month,
                var name when name.Contains("day") => DateTime.Now.Day,
                _ => 1
            };
        }

        private static long GetLongExample(string paramName)
        {
            return paramName switch
            {
                var name when name.Contains("id") => 123456789L,
                var name when name.Contains("timestamp") => DateTimeOffset.Now.ToUnixTimeSeconds(),
                _ => 1L
            };
        }

        private static decimal GetDecimalExample(string paramName)
        {
            return paramName switch
            {
                var name when name.Contains("price") || name.Contains("cost") => 29.99m,
                var name when name.Contains("amount") || name.Contains("total") => 100.50m,
                var name when name.Contains("rate") || name.Contains("percentage") => 15.5m,
                _ => 0.00m
            };
        }

        private static double GetDoubleExample(string paramName)
        {
            return paramName switch
            {
                var name when name.Contains("lat") || name.Contains("latitude") => 40.7128,
                var name when name.Contains("lng") || name.Contains("longitude") => -74.0060,
                var name when name.Contains("score") || name.Contains("rating") => 4.5,
                _ => 1.0
            };
        }

        private static float GetFloatExample(string paramName)
        {
            return (float)GetDoubleExample(paramName);
        }

        private static bool GetBoolExample(string paramName)
        {
            return paramName switch
            {
                var name when name.Contains("active") || name.Contains("enabled") => true,
                var name when name.Contains("deleted") || name.Contains("disabled") => false,
                var name when name.Contains("verified") || name.Contains("confirmed") => true,
                _ => true
            };
        }

        private static DateTime GetDateTimeExample(string paramName)
        {
            return paramName switch
            {
                var name when name.Contains("birth") => new DateTime(1990, 1, 1),
                var name when name.Contains("created") => DateTime.Now,
                var name when name.Contains("updated") || name.Contains("modified") => DateTime.Now,
                var name when name.Contains("expire") || name.Contains("end") => DateTime.Now.AddDays(30),
                _ => DateTime.Now
            };
        }

        private static object? GetEnumExample(Type enumType)
        {
            var values = Enum.GetValues(enumType);
            return values.Length > 0 ? values.GetValue(0) : null;
        }
    }
}
