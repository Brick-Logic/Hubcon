using Hubcon.Client.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public static class ClientSubscriptionExtensions
    {
        public static async ValueTask AddHandler<T>(this ISubscription<T>? sub, Func<T?, Task> handler)
        {
            if (sub is IClientSubscription<T> clientSub) clientSub?.AddHandler(handler);
        }

        public static async ValueTask RemoveHandler<T>(this ISubscription<T>? sub, Func<T?, Task> handler)
        {
            if (sub is IClientSubscription<T> clientSub) clientSub?.RemoveHandler(handler);
        }

        public static async ValueTask Subscribe<T>(this ISubscription<T>? sub)
        {
            if (sub is IClientSubscription<T> clientSub) await clientSub.Subscribe();
        }

        public static async ValueTask Unsubscribe<T>(this ISubscription<T>? sub)
        {
            if (sub is IClientSubscription<T> clientSub) await clientSub.Unsubscribe();
        }
    }
}
