using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HubconAnalyzers.SourceGenerators.Extensions
{
    public static class MethodExtensions
    {
        public static string GetMethodSignature(this IMethodSymbol method)
        {
            List<string> identifiers = new List<string>()
            {
                method.Name
            };

            identifiers.AddRange(method.Parameters.Select(p => p.Type.Name));
            var result = string.Join("_", identifiers);
            return result;
        }
    }
}
