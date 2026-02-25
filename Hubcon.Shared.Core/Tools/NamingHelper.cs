using System.Collections.Concurrent;

namespace Hubcon
{
    public static class NamingHelper
    {
        private static readonly ConcurrentDictionary<string, string> CleanNames = new();

        public static string GetCleanName(string name)
        {
            return CleanNames.GetOrAdd(name, inputName =>
            {
                var cleanedName = inputName;

                if (inputName.EndsWith("Controller"))
                    cleanedName = inputName.Replace("Controller", "");
                if (inputName.EndsWith("Service"))
                    cleanedName = inputName.Replace("Service", "");
                if (inputName.EndsWith("Contract"))
                    cleanedName = inputName.Replace("Contract", "");
                if (inputName.EndsWith("ContractHandler"))
                    cleanedName = inputName.Replace("ContractHandler", "");

                // Verificar si empieza con 'I' y tiene al menos 2 caracteres
                if (cleanedName.Length >= 2 && cleanedName[0] == 'I' && char.IsUpper(cleanedName[1]))
                {
                    cleanedName = cleanedName.Substring(1);
                }

                return cleanedName;
            });
        }
    }
}
