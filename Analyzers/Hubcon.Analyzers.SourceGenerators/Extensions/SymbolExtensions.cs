using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Analyzers.SourceGenerators.Extensions
{
    public static class SymbolExtensions
    {
        /// <summary>
        /// Obtiene el ITypeSymbol de un tipo conocido por su nombre completo.
        /// </summary>
        public static INamedTypeSymbol GetTypeSymbol<T>(this Compilation compilation)
        {
            var type = typeof(T);

            // 1. Intento directo (rápido)
            var symbol = compilation.GetTypeByMetadataName(type.FullName);
            if (symbol != null) return symbol;

            // 2. Intento exhaustivo en referencias (lento pero seguro)
            foreach (var reference in compilation.References)
            {
                var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (assemblySymbol != null)
                {
                    var found = assemblySymbol.GetTypeByMetadataName(type.FullName);
                    if (found != null) return found;
                }
            }

            return null;
        }
    }
}
