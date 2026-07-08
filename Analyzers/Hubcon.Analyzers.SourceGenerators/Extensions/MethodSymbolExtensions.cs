using Hubcon.Shared.Abstractions.Standard.Extensions;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;
using Hubcon.Analyzers.SourceGenerators.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;

namespace HubconAnalyzers.SourceGenerators.Extensions
{
    public static class MethodSymbolExtensions
    {
        public static string GetMethodSymbolSignature(this IMethodSymbol method, bool useHashed = true)
        {
            string methodName = method.Name;
            string parameters = string.Empty;

            if (method.Parameters.Length > 0)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    if (i > 0)
                        builder.Append(", ");

                    var param = method.Parameters[i];

                    if (param.RefKind == RefKind.Ref)
                        builder.Append("ref ");
                    else if (param.RefKind == RefKind.Out)
                        builder.Append("out ");

                    builder.Append(GetSymbolTypeString(param.Type));
                }

                parameters = $"({builder})";
            }

            return useHashed
                ? MethodExtensions.ToHashedMethodString(methodName, parameters)
                : $"{methodName}{parameters}";
        }

        private static string GetSymbolTypeString(ITypeSymbol type)
        {
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    {
                        var commas = new string(',', arrayType.Rank - 1);
                        return $"{GetSymbolTypeString(arrayType.ElementType)}[{commas}]";
                    }

                case INamedTypeSymbol namedType:
                    {
                        var sb = new StringBuilder();

                        if (!namedType.ContainingNamespace.IsGlobalNamespace)
                        {
                            sb.Append(namedType.ContainingNamespace.ToDisplayString());
                            sb.Append(".");
                        }

                        // Nested types handling
                        var containingType = namedType.ContainingType;
                        if (containingType != null)
                        {
                            sb.Append(GetSymbolTypeString(containingType));
                            sb.Append(".");
                            sb.Append(namedType.Name);
                        }
                        else
                        {
                            sb.Append(namedType.Name);
                        }

                        if (namedType.TypeArguments.Length > 0)
                        {
                            sb.Append("<");

                            for (int i = 0; i < namedType.TypeArguments.Length; i++)
                            {
                                if (i > 0)
                                    sb.Append(", ");

                                sb.Append(GetSymbolTypeString(namedType.TypeArguments[i]));
                            }

                            sb.Append(">");
                        }

                        return sb.ToString();
                    }

                case ITypeParameterSymbol typeParam:
                    return typeParam.Name;

                default:
                    return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                               .Replace("global::", "")
                               .Replace('+', '.');
            }
        }

        public static string GetHubconResponseTypeFromMethod(this IMethodSymbol method)
        {
            var returnType = method.ReturnType.ToDisplayString();
            
            if (returnType == "void")
            {
                return "Hubcon.HubconResponse";
            } 
            
            if (returnType.StartsWith("Hubcon.HubconResponse<"))
            {
                // Streaming
                return returnType;
            } 
            
            if (returnType == "Hubcon.HubconResponse")
            {
                return "Hubcon.HubconResponse";
            } 
            
            if (returnType.StartsWith("System.Collections.Generic.IAsyncEnumerable<"))
            {
                return returnType;
            } 
            
            if (returnType.StartsWith("System.Threading.Tasks.Task<"))
            {
                var generic = returnType.GetGenericArgument("System.Threading.Tasks.Task");
                
                if (generic.StartsWith("Hubcon.HubconResponse<"))
                {
                    return generic;
                } 
            
                if (returnType == "Hubcon.HubconResponse")
                {
                    return "Hubcon.HubconResponse";
                } 
                
                return $"Hubcon.HubconResponse<{generic}>";
            }
            
            if (returnType == "System.Threading.Tasks.Task")
            {
                return "Hubcon.HubconResponse";
            }
            
            return $"Hubcon.HubconResponse<{returnType}>";
        } 

        //public static string GetMethodSymbolSignature(this IMethodSymbol method)
        //{
        //    string GetRuntimeTypeName(ITypeSymbol type)
        //    {
        //        if (type is IArrayTypeSymbol arrayType)
        //        {
        //            return $"{GetRuntimeTypeName(arrayType.ElementType)}[]";
        //        }

        //        if (type is INamedTypeSymbol named)
        //        {
        //            if (named.IsGenericType)
        //            {
        //                var typeArgs = string.Join(",",
        //                    named.TypeArguments.Select(GetRuntimeTypeName));

        //                var baseName = $"{named.ContainingNamespace}.{named.Name}`{named.TypeArguments.Length}";
        //                return $"{baseName}[{typeArgs}]";
        //            }
        //            else
        //            {
        //                return $"{named.ContainingNamespace}.{named.Name}";
        //            }
        //        }

        //        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
        //    }

        //    var parameters = string.Join(", ", method.Parameters.Select(p => GetRuntimeTypeName(p.Type)));

        //    if (method.Parameters.Length > 0)
        //        parameters = $"({parameters})";

        //    return $"{method.Name}{parameters}";
        //}
    }
}