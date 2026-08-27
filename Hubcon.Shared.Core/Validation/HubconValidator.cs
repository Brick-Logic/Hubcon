using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace System.Diagnostics.CodeAnalysis.Validation
{
    public static class HubconValidator
    {
        private readonly static ConcurrentDictionary<Type, ValidationMetadata> _validationMetadata = new();

        public static bool TryValidate<T>(T value, out List<ValidationResult> validationResults)
        {
            validationResults = new List<ValidationResult>();

            if (value is null)
            {
                return true;
            }

            var visitedObjects = new HashSet<object>();
            ValidateObjectRecursive(value, validationResults, visitedObjects, parentPath: string.Empty);

            return validationResults.Count == 0;
        }

        private static void ValidateObjectRecursive(
            object instance,
            List<ValidationResult> validationResults,
            HashSet<object> visitedObjects,
            string parentPath)
        {
            if (instance is null) return;

            // Evitar ciclos infinitos por referencias circulares
            if (!visitedObjects.Add(instance)) return;

            var type = instance.GetType();

            // Obtener propiedades públicas de instancia descartando [JsonIgnore]
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !p.IsDefined(typeof(JsonIgnoreAttribute), inherit: true));

            foreach (var property in properties)
            {
                // Evitar indexadores (ej: this[int index])
                if (property.GetIndexParameters().Length > 0) continue;

                object? propertyValue = property.GetValue(instance);
                string currentPath = string.IsNullOrEmpty(parentPath)
                    ? property.Name
                    : $"{parentPath}.{property.Name}";

                // 1. Validar los [ValidationAttribute] de la propiedad actual
                var validationAttributes = property.GetCustomAttributes<ValidationAttribute>(inherit: true);
                var context = new ValidationContext(instance) { MemberName = currentPath };

                foreach (var attribute in validationAttributes)
                {
                    var result = attribute.GetValidationResult(propertyValue, context);
                    if (result != ValidationResult.Success && result is not null)
                    {
                        // Ajustar el MemberName en el resultado para reflejar la ruta completa
                        var memberNames = result.MemberNames.Any() ? result.MemberNames : new[] { currentPath };
                        validationResults.Add(new ValidationResult(result.ErrorMessage, memberNames));
                    }
                }

                // 2. Si el valor es nulo, no descendemos recursivamente
                if (propertyValue is null) continue;

                var propType = propertyValue.GetType();

                // Descartar tipos primitivos, strings, decimales, DateTime, Enums, etc.
                if (IsSimpleType(propType)) continue;

                // 3. Manejo de Colecciones / Listas / Arrays
                if (propertyValue is IEnumerable enumerable && propType != typeof(string))
                {
                    int index = 0;
                    foreach (var item in enumerable)
                    {
                        if (item is not null && !IsSimpleType(item.GetType()))
                        {
                            string itemPath = $"{currentPath}[{index}]";
                            ValidateObjectRecursive(item, validationResults, visitedObjects, itemPath);
                        }

                        index++;
                    }
                }
                // 4. Propiedades compuestas (Clases / Structs)
                else
                {
                    ValidateObjectRecursive(propertyValue, validationResults, visitedObjects, currentPath);
                }
            }
        }

        private static bool IsSimpleType(Type type)
        {
            {
                var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

                return underlyingType.IsPrimitive
                       || underlyingType.IsEnum
                       || underlyingType == typeof(string)
                       || underlyingType == typeof(decimal)
                       || underlyingType == typeof(DateTime)
                       || underlyingType == typeof(DateTimeOffset)
                       || underlyingType == typeof(TimeSpan)
                       || underlyingType == typeof(Guid);
            }
        }
    }

    public sealed class ValidationMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field,
        AllowMultiple = false)]
    public class ValidateObjectAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            var results = new List<ValidationResult>();
            var context = new ValidationContext(value, null, null);

            if (!Validator.TryValidateObject(value, context, results, validateAllProperties: true))
            {
                var compositeResults =
                    new CompositeValidationResult($"Validation failed for {validationContext.DisplayName}");
                results.ForEach(compositeResults.AddResult);
                return compositeResults;
            }

            return ValidationResult.Success;
        }
    }

    // Clase contenedora para amalgamar los errores si querés el árbol completo
    public class CompositeValidationResult : ValidationResult
    {
        private readonly List<ValidationResult> _results = new();
        public IEnumerable<ValidationResult> Results => _results;

        public CompositeValidationResult(string errorMessage) : base(errorMessage)
        {
        }

        public void AddResult(ValidationResult result) => _results.Add(result);
    }
}