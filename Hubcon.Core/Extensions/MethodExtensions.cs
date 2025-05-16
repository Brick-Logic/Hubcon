using Autofac;
using Autofac.Builder;
using Castle.Core.Internal;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Attributes;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using System.Diagnostics.Contracts;
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

                    if (prop.PropertyType.IsAssignableTo(typeof(ISubscription)))
                    {
                        var accessor = e.Context.ResolveOptional<IHttpContextAccessor>();

                        if (accessor != null)
                        {
                            var contract = prop.ReflectedType!
                                .GetInterfaces()
                                .Find(x => x.IsAssignableTo(typeof(IControllerContract))).Name;

                            var operationRegistry = e.Context.ResolveOptional<IOperationRegistry>();

                            if (!operationRegistry!.GetOperationBlueprint(contract, prop.Name, out IOperationBlueprint? blueprint))
                                continue;

                            if (blueprint?.Kind != OperationKind.Subscription)
                                continue;

                            var sub = e.Context.Resolve<ILiveSubscriptionRegistry>();

                            if (blueprint.RequiresAuthorization)
                            {
                                var token = JwtHelper.ExtractTokenFromHeader(accessor.HttpContext);
                                var userId = JwtHelper.GetUserId(token);

                                var descriptor = sub.GetHandler(userId!, contract, prop.Name);
                                AssignProperty(e.Instance, prop, descriptor?.Subscription);
                            }
                            else
                            {
                                var descriptor = sub.GetHandler("", contract, prop.Name);
                                AssignProperty(e.Instance, prop, descriptor?.Subscription);
                            }
                        }
                        else
                        {
                            var resolved = e.Context.ResolveOptional(prop.PropertyType);

                            var resolvedSubscription = (ISubscription?)resolved!;

                            if (resolvedSubscription != null && resolvedSubscription.Property == null)
                            {
                                AssignProperty(resolvedSubscription, nameof(resolvedSubscription.Property), prop);
                                resolvedSubscription?.Build();
                            }

                            AssignProperty(e.Instance, prop, resolvedSubscription);
                        }
                    }
                    else
                    {
                        var resolved = e.Context.ResolveOptional(prop.PropertyType);
                        AssignProperty(e.Instance, prop, resolved);
                    }           
                }
            });

            return container;
        }

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
