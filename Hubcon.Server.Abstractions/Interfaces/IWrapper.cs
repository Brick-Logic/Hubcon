using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Hubcon
{
    public interface IWrapper
    {
        public void Populate(IReadOnlyDictionary<string, object> parameters);
        
        public bool TryValidate(out IEnumerable<ValidationResult> errors);
    }
}