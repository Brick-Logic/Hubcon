using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using System.Reflection;

namespace Hubcon.Server.Core.Injectors
{
    public static class HubconInjector
    {
        public static bool GetServiceWithInjector<T>(this IServiceProvider serviceProvider, T instance, Action<IDependencyInjector<T, object?>>? configure = null)
        {
            var injector = new DependencyInjector<T, object?>(serviceProvider, instance!);

            configure?.Invoke(injector);

            var properties = instance!.GetType().BaseType!
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(prop => Attribute.IsDefined(prop, typeof(HubconInjectAttribute)));

            foreach (var property in properties)
            {
                if (property.GetValue(instance) != null)
                    continue;

                injector.Inject(property).WithType(property.PropertyType);
            }

            return true;
        }
    }
}
