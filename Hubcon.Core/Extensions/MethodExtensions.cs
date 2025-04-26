using Autofac.Builder;
using Autofac;
using System.ComponentModel;
using System.Reflection;
using Hubcon.Core.Injectors.Attributes;

namespace Hubcon.Core.Extensions
{
    public static class HubconExtensions
    {
        public static ContainerBuilder RegisterWithInjector<TType, TActivatorData, TSingleRegistrationStyle>(
            this ContainerBuilder container,
            Func<ContainerBuilder, IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle>>? options = null)
        {
            var registered = options?.Invoke(container);
            registered?.OnActivated(e =>
            {
                List<PropertyInfo> props = new();

                props.AddRange(e.Instance!.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(prop => Attribute.IsDefined(prop, typeof(HubconInjectAttribute)))
                    .ToList());

                props.AddRange(e.Instance!.GetType().BaseType!
                    .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(p => p.IsDefined(typeof(HubconInjectAttribute), false))
                    .ToList());

                foreach (PropertyInfo prop in props!)
                {
                    if (prop.GetValue(e.Instance) != null)
                        continue;

                    var resolved = e.Context.ResolveOptional(prop.PropertyType);

                    if (resolved == null)
                        continue;

                    var instance = e.Instance;

                    var setMethod = prop!.GetSetMethod(true);
                    if (setMethod != null)
                    {
                        setMethod.Invoke(instance, new[] { resolved });
                    }
                    else
                    {
                        // Si no tiene setter, usamos el campo backing
                        var field = prop!.DeclaringType?.GetField($"<{prop.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);

                        field?.SetValue(instance, resolved);
                    }
                }
            });

            return container;
        }

        public static IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> AsScoped<TType, TActivatorData, TSingleRegistrationStyle>(this IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> regBuilder)
            => regBuilder.InstancePerLifetimeScope();

        public static IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> AsTransient<TType, TActivatorData, TSingleRegistrationStyle>(this IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> regBuilder)
            => regBuilder.InstancePerDependency();

        public static IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> AsSingleton<TType, TActivatorData, TSingleRegistrationStyle>(this IRegistrationBuilder<TType, TActivatorData, TSingleRegistrationStyle> regBuilder)
            => regBuilder.SingleInstance();

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
