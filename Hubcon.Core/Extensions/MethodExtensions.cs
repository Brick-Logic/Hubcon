using Autofac;
using Autofac.Builder;
using Castle.Core.Internal;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.Models.CustomAttributes.Subscriptions;
using Hubcon.Core.Models.Interfaces;
using System.Reflection;

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
                    .Where(prop => Attribute.IsDefined(prop, typeof(HubconInjectAttribute)) || prop.PropertyType.IsAssignableTo(typeof(ISubscription)))
                    .ToList());

                props.AddRange(e.Instance!.GetType().BaseType!
                    .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(prop => prop.IsDefined(typeof(HubconInjectAttribute), false) || prop.PropertyType.IsAssignableTo(typeof(ISubscription)))
                    .ToList());

                foreach (PropertyInfo prop in props!)
                {
                    if (prop.GetValue(e.Instance) != null)
                        continue;

                    object? resolved = null!;

                    if (prop.PropertyType.IsAssignableTo(typeof(ISubscription)))
                    {
                        var sub = e.Context.ResolveOptional<ISubscriptionRegistry>();
                        var contract = prop.ReflectedType!.GetInterfaces().Find(x => x.IsAssignableTo(typeof(IControllerContract))).Name;
                        resolved = sub?.GetHandler("broadcast", contract, prop.Name);

                        if (resolved == null)
                        {
                            resolved = e.Context.ResolveOptional(prop.PropertyType);
                            sub?.RegisterHandler("broadcast", contract, prop.Name, (ISubscription)resolved!);
                        }

                        var resolvedSubscription = (ISubscription)resolved!;

                        if (resolvedSubscription.Property == null)
                        {
                            var field = resolvedSubscription!.GetType().GetField($"<{nameof(resolvedSubscription.Property)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                            field?.SetValue(resolvedSubscription, prop);
                        }

                        resolvedSubscription.Build();
                    }

                    resolved ??= e.Context.ResolveOptional(prop.PropertyType);

                    if (resolved == null)
                        return;

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
