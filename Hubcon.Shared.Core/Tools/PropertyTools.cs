using System.Reflection;

namespace Hubcon.Shared.Core.Tools
{
    public static class PropertyTools
    {
        public static void AssignProperty(object instance, PropertyInfo prop, object? value)
        {
            if (value == null)
                return;

            var setMethod = prop!.GetSetMethod(true);
            if (setMethod != null)
            {
                setMethod.Invoke(instance, new[] { value });
            }
            else
            {
                // Si no tiene setter, usamos el campo backing
                var field = prop!.DeclaringType?.GetField($"<{prop.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                field?.SetValue(instance, value);
            }
        }

        public static void AssignProperty(object instance, string propName, object? value)
        {
            try
            {
                if (value == null)
                    return;

                // Si no tiene setter, usamos el campo backing
                var field = instance.GetType()!.GetField($"<{propName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                field?.SetValue(instance, value);
            }
            catch
            {
                return;
            }
        }
    }
}
