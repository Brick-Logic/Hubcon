#pragma warning disable CS1591
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
