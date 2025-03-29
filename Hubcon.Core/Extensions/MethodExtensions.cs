using System.ComponentModel;
using System.Reflection;

namespace Hubcon.Core.Extensions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class HubconExtensions
    {
        public static string GetMethodSignature(this MethodInfo method)
        {
            List<string> identifiers = new()
            {
                method.ReturnType.Name,
                method.Name
            };
            identifiers.AddRange(method.GetParameters().Select(p => p.ParameterType.Name));
            var result = string.Join("_", identifiers);
            return result;
        }
    }
}
