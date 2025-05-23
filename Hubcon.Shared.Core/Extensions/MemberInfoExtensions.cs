using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Extensions
{
    public static class MemberInfoExtensions
    {
        private static readonly Dictionary<string, bool> _attributeCache = new();

        public static bool HasCustomAttribute<TCustomAttribute>(this MemberInfo member) where TCustomAttribute : Attribute
        {
            var methodName = $"{member.ReflectedType!.Name}_{member.Name}";
            if (!_attributeCache.TryGetValue(methodName, out var hasAttribute))
            {
                hasAttribute = member.IsDefined(typeof(TCustomAttribute), false);
                _attributeCache[methodName] = hasAttribute;
            }
            return hasAttribute;
        }
    }
}
