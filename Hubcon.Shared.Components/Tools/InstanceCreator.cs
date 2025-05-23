using System.Reflection;

namespace Hubcon.Shared.Components.Tools
{
    public static class InstanceCreator
    {
        public static T TryCreateInstance<T>(params object[] parameters)
        {
            List<Type> types = new();

            foreach (var parameter in parameters)
            {
                types.Add(parameter.GetType());
            }

            var constructor = typeof(T)
                .GetConstructor(
                    BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null,
                    types.ToArray(),
                    null
                );

            if (constructor == null) return (T)Activator.CreateInstance(typeof(T), true)!;

            return (T)constructor.Invoke(parameters);
        }

        public static object? TryCreateInstance(Type type, params object[] parameters)
        {
            List<Type> types = new();

            foreach (var parameter in parameters)
            {
                types.Add(parameter.GetType());
            }

            var constructor = type
                .GetConstructor(
                    BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null,
                    types.ToArray(),
                    null
                );

            if (constructor == null) return Activator.CreateInstance(type, true)!;

            return constructor.Invoke(parameters);
        }
    }
}
