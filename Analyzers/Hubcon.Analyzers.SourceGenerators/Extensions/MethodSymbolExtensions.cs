using Hubcon.Shared.Abstractions.Standard.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HubconAnalyzers.SourceGenerators.Extensions
{
    public static class MethodSymbolExtensions
    {
        public static string GetMethodSymbolSignature(this IMethodSymbol method)
        {
            string GetRuntimeTypeName(ITypeSymbol type)
            {
                if (type is IArrayTypeSymbol arrayType)
                {
                    return $"{GetRuntimeTypeName(arrayType.ElementType)}[]";
                }

                if (type is INamedTypeSymbol named)
                {
                    if (named.IsGenericType)
                    {
                        var typeArgs = string.Join(",",
                            named.TypeArguments.Select(GetRuntimeTypeName));

                        var baseName = $"{named.ContainingNamespace}.{named.Name}`{named.TypeArguments.Length}";
                        return $"{baseName}[{typeArgs}]";
                    }
                    else
                    {
                        return $"{named.ContainingNamespace}.{named.Name}";
                    }
                }

                return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
            }

            var parameters = string.Join(", ", method.Parameters.Select(p => GetRuntimeTypeName(p.Type)));

            if (method.Parameters.Length > 0)
                parameters = $"({parameters})";

            return $"{method.Name}{parameters}";
        }
    }
}