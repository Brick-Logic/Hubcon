using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Hubcon.Validation;

namespace Hubcon
{
    [HubconPreserve]
    public sealed class UseNodeValidatorAttribute : Attribute
    {
    }
    
    public static class NodeValidator
    {
        public static bool TryValidate<T>(T value, List<ValidationResult> results)
        {
            var validator = NodeValidatorProvider.GetValidator<T>();
            return validator != null && validator.TryValidate(value, results);
        }
    }
}