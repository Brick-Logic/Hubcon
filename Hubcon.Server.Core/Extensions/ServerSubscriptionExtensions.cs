using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public static class ServerSubscriptionExtensions
    {
        public static async ValueTask Emit<T>(this ISubscription<T>? sub, T? value)
        {
            if (sub is IServerSubscription<T> serverSubscription) await serverSubscription.Emit(value);
        }
    }
}

namespace Hubcon.Server.Core.Extensions
{
    internal static class ServerSubscriptionExtensions
    {
        public static async ValueTask AddGenericHandler(this ISubscription? sub, Func<object?, Task> handler)
        {
            if (sub is IServerSubscription serverSubscription) await serverSubscription.AddGenericHandler(handler);
        }

        public static async ValueTask RemoveGenericHandler(this ISubscription? sub, Func<object?, Task> handler)
        {
            if (sub is IServerSubscription serverSubscription) await serverSubscription.RemoveGenericHandler(handler);
        }

        public static async ValueTask EmitGeneric(this ISubscription? sub, object? eventValue)
        {
            if (sub is IServerSubscription serverSubscription) await serverSubscription.EmitGeneric(eventValue);
        }
    }
}