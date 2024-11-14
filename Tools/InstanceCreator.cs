using System.Reflection;

namespace Hubcon.Tools
{
    public static class InstanceCreator
    {
        public static T TryCreateInstance<T>(params object[] parameters)
        {
            List<Type> types = [];

            foreach(var parameter in parameters)
            {
                types.Add(parameter.GetType());
            }

            ConstructorInfo? constructor = typeof(T)
                .GetConstructor(
                    BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null,
                    [.. types],
                    null
                );

            if (constructor == null) return (T)Activator.CreateInstance(typeof(T), true)!;

            return (T)constructor.Invoke(parameters);
        }
    }
}
