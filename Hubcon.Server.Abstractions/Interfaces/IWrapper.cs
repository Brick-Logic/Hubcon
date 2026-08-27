using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hubcon
{
    public interface IWrapper
    {
        public void Populate(IReadOnlyDictionary<string, object> parameters);
        public bool TryValidate(out List<ValidationResult> errors);
    }
}