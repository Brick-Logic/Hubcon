using Autofac;
using Autofac.Builder;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Hubcon.Server.Core.Extensions
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

                    if (prop.ReflectedType!.IsAssignableTo(typeof(BaseContractProxy)))
                        continue;

                    if (prop.PropertyType.IsAssignableTo(typeof(ISubscription)) && prop.ReflectedType!.IsAssignableTo(typeof(IControllerContract)))
                    {
                        var accessor = e.Context.ResolveOptional<IHttpContextAccessor>();

                        if (accessor != null)
                        {
                            var contract = prop.ReflectedType?
                                .GetInterfaces()?
                                .ToList()?
                                .Find(x => x.IsAssignableTo(typeof(IControllerContract)))?.Name;

                            var operationRegistry = e.Context.Resolve<IOperationRegistry>();

                            if (!operationRegistry.GetOperationBlueprint(contract!, prop.Name, out IOperationBlueprint? blueprint))
                                continue;

                            if (blueprint?.Kind != OperationKind.Subscription)
                                continue;

                            var sub = e.Context.Resolve<ILiveSubscriptionRegistry>();

                            if (blueprint.RequiresAuthorization)
                            {
                                var token = JwtHelper.ExtractTokenFromHeader(accessor.HttpContext);
                                var userId = JwtHelper.GetUserId(token);

                                var descriptor = sub.GetHandler(userId!, contract!, prop.Name);
                                PropertyTools.AssignProperty(e.Instance, prop, descriptor?.Subscription);
                            }
                            else
                            {
                                var descriptor = sub.GetHandler("", contract!, prop.Name);
                                PropertyTools.AssignProperty(e.Instance, prop, descriptor?.Subscription);
                            }
                        }
                        else
                        {
                            var resolved = e.Context.ResolveOptional(prop.PropertyType);

                            var resolvedSubscription = (ISubscription?)resolved!;

                            if (resolvedSubscription != null && resolvedSubscription.Property == null)
                            {
                                PropertyTools.AssignProperty(resolvedSubscription, nameof(resolvedSubscription.Property), prop);
                                resolvedSubscription?.Build();
                            }

                            PropertyTools.AssignProperty(e.Instance, prop, resolvedSubscription);
                        }
                    }
                    else
                    {
                        var resolved = e.Context.ResolveOptional(prop.PropertyType);
                        PropertyTools.AssignProperty(e.Instance, prop, resolved);
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
    }
}
