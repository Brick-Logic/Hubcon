using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Hubcon.Analyzers
{
    public static class Tools
    {
        internal static bool InControllerScope(INamedTypeSymbol type)
        {
            if (type == null)
                return false;

            // Caso 1: si es una INTERFAZ, solo entra si hereda (indirectamente) de IControllerContract
            if (type.TypeKind == TypeKind.Interface)
            {
                return type.AllInterfaces.Any(i => i.Name == "IControllerContract");
            }

            // Caso 2: si es una CLASE, verificamos que implemente una interfaz que a su vez
            // hereda de IControllerContract. Pero NO aceptamos implementación directa ni por BaseType.
            if (type.TypeKind == TypeKind.Class)
            {
                foreach (var iface in type.AllInterfaces)
                {
                    if (iface.Name == "IControllerContract")
                        continue; // descartar implementación directa

                    if (iface.AllInterfaces.Any(i => i.Name == "IControllerContract"))
                        return true; // acepta solo si es por interfaz derivada
                }
            }

            return false;
        }

    }
}
