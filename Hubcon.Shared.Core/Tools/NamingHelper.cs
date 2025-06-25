using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Tools
{
    public static class NamingHelper
    {
        public static string GetCleanName(string name)
        {
            var cleanedName = name;

            if (name.EndsWith("Controller"))
                cleanedName = name.Replace("Controller", "");
            if (name.EndsWith("Service"))
                cleanedName = name.Replace("Service", "");
            if (name.EndsWith("Contract"))
                cleanedName = name.Replace("Contract", "");
            if (name.EndsWith("ContractHandler"))
                cleanedName = name.Replace("ContractHandler", "");

            // Verificar si empieza con 'I' y tiene al menos 2 caracteres
            if (cleanedName.Length >= 2 &&
                cleanedName[0] == 'I' &&
                char.IsUpper(cleanedName[1]))
            {
                cleanedName = cleanedName.Substring(1);
            }

            return cleanedName;
        }
    }
}
