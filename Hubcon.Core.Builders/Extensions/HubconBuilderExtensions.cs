using Autofac;
using Autofac.Extensions.DependencyInjection;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Connectors;
using Hubcon.Core.Controllers;
using Hubcon.Core.Extensions;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Invocation;
using Hubcon.Core.Pipelines;
using Hubcon.Core.Routing.MethodHandling;
using Hubcon.Core.Routing.Registries;
using Hubcon.Core.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hubcon.Core.Builders.Extensions
{
    public static class HubconBuilderExtensions
    {
        private static List<Action<ContainerBuilder>> ServicesToInject { get; } = new();
        private static readonly IProxyRegistry Proxies = new ProxyRegistry();
        private static readonly ILiveSubscriptionRegistry SubscriptionRegistry = new LiveSubscriptionRegistry();
        private static readonly List<Type> ProxiesToRegister = new();
        private static readonly List<Type> ControllersToRegister = new();
        private static readonly IOperationRegistry OperationRegistry = new OperationRegistry();

    }
}
