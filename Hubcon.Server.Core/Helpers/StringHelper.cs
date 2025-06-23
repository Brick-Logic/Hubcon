using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Helpers
{
    public static class StringHelper
    {
        public static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Property";

            return char.ToUpper(input[0]) + input.Substring(1);
        }
    }
}
