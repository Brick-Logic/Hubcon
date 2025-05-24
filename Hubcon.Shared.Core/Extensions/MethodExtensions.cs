using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Extensions
{
    public static class MethodExtensions
    {
        public static string GetMethodSignature(this MethodInfo method)
        {
            List<string> identifiers = new()
            {
                method.ReturnType.Name,
                method.Name
            };

            identifiers.AddRange(method.GetParameters().Select(p => p.ParameterType.Name));
            var result = string.Join("_", identifiers);
            return result;
        }
    }
}
