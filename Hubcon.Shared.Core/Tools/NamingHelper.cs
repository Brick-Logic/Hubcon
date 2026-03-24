#pragma warning disable CS1591
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

                if (inputName.Contains("Controller"))
                    cleanedName = inputName.Replace("Controller", "");
                if (inputName.Contains("Service"))
                    cleanedName = inputName.Replace("Service", "");
                if (inputName.Contains("ContractHandler"))
                    cleanedName = inputName.Replace("ContractHandler", "");
                if (inputName.Contains("Contract"))
                    cleanedName = inputName.Replace("Contract", "");

                if (cleanedName.Length >= 2 && cleanedName[0] == 'I' && char.IsUpper(cleanedName[1]))
                {
                    cleanedName = cleanedName.Substring(1);
                }

                return cleanedName;
            });
        }
    }
}
