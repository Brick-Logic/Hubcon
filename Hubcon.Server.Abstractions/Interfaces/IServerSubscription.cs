using Hubcon.Shared.Abstractions.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IServerSubscription : ISubscription
    {
        public ValueTask AddGenericHandler(Func<object?, Task> handler);
        public ValueTask RemoveGenericHandler(Func<object?, Task> handler);
        public ValueTask EmitGeneric(object? eventValue);
    }

    public interface IServerSubscription<T> : IServerSubscription, ISubscription<T>
    {
        public PropertyInfo Property { get; }

        public ConcurrentDictionary<object, Func<object?, Task>> Handlers { get; }

        public event Func<object?, Task>? OnEventReceived;

        public ValueTask AddHandler(Func<T?, Task> handler);

        public ValueTask RemoveHandler(Func<T?, Task> handler);

        public ValueTask Emit(T? eventValue);   

        public static IServerSubscription<T> operator +(IServerSubscription<T> handler, Func<T?, Task> hubconEventHandler)
        {
            handler.AddHandler(hubconEventHandler);
            return handler;
        }

        public static IServerSubscription<T> operator +(IServerSubscription<T> handler, Func<object?, Task> hubconEventHandler)
        {
            handler.AddGenericHandler(hubconEventHandler.Invoke);
            return handler;
        }

        public static IServerSubscription<T> operator -(IServerSubscription<T> handler, Func<T?, Task> hubconEventHandler)
        {
            handler.RemoveHandler(hubconEventHandler);
            return handler;
        }

        public static IServerSubscription<T> operator -(IServerSubscription<T> handler, Func<object?, Task> hubconEventHandler)
        {
            handler.RemoveGenericHandler(hubconEventHandler);
            return handler;
        }
    }
}
