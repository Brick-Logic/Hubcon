using Hubcon.Shared.Abstractions.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientSubscription<T> : ISubscription<T>
    {
        public event Func<object?, Task>? OnEventReceived;
        public SubscriptionState Connected { get; }

        ValueTask Subscribe();
        ValueTask Unsubscribe();

        public ConcurrentDictionary<object, Func<object?, Task>> Handlers { get; }

        public ValueTask AddHandler(Func<T?, Task> handler);
        public ValueTask RemoveHandler(Func<T?, Task> handler);

        public static IClientSubscription<T> operator +(IClientSubscription<T> handler, Func<T?, Task> hubconEventHandler)
        {
            handler.AddHandler(hubconEventHandler);
            return handler;
        }

        public static IClientSubscription<T> operator +(IClientSubscription<T> handler, Func<object?, Task> hubconEventHandler)
        {
            handler.AddGenericHandler(hubconEventHandler);
            return handler;
        }

        public static IClientSubscription<T> operator -(IClientSubscription<T> handler, Func<T?, Task> hubconEventHandler)
        {
            handler.RemoveHandler(hubconEventHandler);
            return handler;
        }

        public static IClientSubscription<T> operator -(IClientSubscription<T> handler, Func<object?, Task> hubconEventHandler)
        {
            handler.RemoveGenericHandler(hubconEventHandler);
            return handler;
        }

        PropertyInfo Property { get; set; }

        public ValueTask AddGenericHandler(Func<object?, Task> handler);
        public ValueTask RemoveGenericHandler(Func<object?, Task> handler);
        public ValueTask EmitGeneric(object? eventValue);
    }
}