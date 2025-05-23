using Hubcon.Shared.Abstractions.Enums;
using System.Reflection;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    //public delegate Task HubconEventHandler(object? eventValue);
    public delegate Task HubconEventHandler<in T>(T? eventValue);

    public interface ISubscription
    {
        PropertyInfo Property { get; }

        public void AddGenericHandler(HubconEventHandler<object> handler);
        public void RemoveGenericHandler(HubconEventHandler<object> handler);
        public void EmitGeneric(object? eventValue);

        void Build();

        public static ISubscription operator +(ISubscription handler, HubconEventHandler<object> hubconEventHandler)
        {
            handler.AddGenericHandler(hubconEventHandler);
            return handler;
        }

        public static ISubscription operator -(ISubscription handler, HubconEventHandler<object> hubconEventHandler)
        {
            handler.RemoveGenericHandler(hubconEventHandler);
            return handler;
        }
    }

    public interface ISubscription<T> : ISubscription
    {
        public event HubconEventHandler<object>? OnEventReceived;
        public SubscriptionState Connected { get; }

        Task Subscribe();
        Task Unsubscribe();

        public Dictionary<object, HubconEventHandler<object>> Handlers { get; }

        public void AddHandler(HubconEventHandler<T> handler);
        public void RemoveHandler(HubconEventHandler<T> handler);
        public void Emit(T? eventValue);

        public static ISubscription<T> operator +(ISubscription<T> handler, HubconEventHandler<T> hubconEventHandler)
        {
            handler.AddHandler(hubconEventHandler);
            return handler;
        }

        public static ISubscription<T> operator +(ISubscription<T> handler, HubconEventHandler<object> hubconEventHandler)
        {
            handler.AddGenericHandler(hubconEventHandler);
            return handler;
        }

        public static ISubscription<T> operator -(ISubscription<T> handler, HubconEventHandler<T> hubconEventHandler)
        {
            handler.RemoveHandler(hubconEventHandler);
            return handler;
        }

        public static ISubscription<T> operator -(ISubscription<T> handler, HubconEventHandler<object> hubconEventHandler)
        {
            handler.RemoveGenericHandler(hubconEventHandler);
            return handler;
        }
    }
}
