using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Hubcon.Server.Core.Subscriptions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ServerSubscriptionHandler<T> : ISubscription<T>
    {
        public PropertyInfo Property { get; } = null!;


        private SubscriptionState _connected = SubscriptionState.Emitter;
        public SubscriptionState Connected => _connected;

        public ConcurrentDictionary<object, HubconEventHandler<object>> Handlers { get; } = new();

        public event HubconEventHandler<object>? OnEventReceived;

        public void AddHandler(HubconEventHandler<T> handler)
        {
            Task internalHandler(object? value) => handler.Invoke((T?)value!);
            Handlers[handler] = internalHandler;
            OnEventReceived += internalHandler;
        }

        public void AddGenericHandler(HubconEventHandler<object> handler)
        {
            Task internalHandler(object? value) => handler.Invoke((T?)value!);
            Handlers[handler] = internalHandler;
            OnEventReceived += internalHandler;
        }

        public void RemoveHandler(HubconEventHandler<T> handler)
        {
            var internalHandler = Handlers[handler];
            OnEventReceived -= internalHandler;
            Handlers.TryRemove(handler, out _);
        }

        public void RemoveGenericHandler(HubconEventHandler<object> handler)
        {
            var internalHandler = Handlers[handler];
            OnEventReceived -= internalHandler;
            Handlers.TryRemove(handler, out _);
        }

        public Task Subscribe()
        {
            return Task.CompletedTask;
        }

        public Task Unsubscribe()
        {
            return Task.CompletedTask;
        }

        public void Build()
        {
        }

        public void Emit(T? eventValue)
        {
            OnEventReceived?.Invoke(eventValue);
        }

        public void EmitGeneric(object? eventValue)
        {
            OnEventReceived?.Invoke((T?)eventValue);
        }
    }
}
