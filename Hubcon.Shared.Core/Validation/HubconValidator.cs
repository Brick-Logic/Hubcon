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