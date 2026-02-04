using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Hubcon.Server.Core.Subscriptions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ServerSubscriptionHandler<T> : ISubscription<T>, IServerSubscription<T>
    {
        PropertyInfo IServerSubscription<T>.Property { get; } = null!;

        ConcurrentDictionary<object, Func<object?, Task>> IServerSubscription<T>.Handlers { get; } = new();

        public event Func<object?, Task>? OnEventReceived;

        async ValueTask IServerSubscription<T>.AddHandler(Func<T?, Task> handler)
        {
            async Task WrapHandler(object? value) => await handler.Invoke((T?)value!);

            if (this is IServerSubscription<T> clientSubscription && !clientSubscription.Handlers.TryGetValue(handler, out var _))
            {
                clientSubscription.Handlers[handler] = WrapHandler;
                OnEventReceived += WrapHandler;
            }
        }

        async ValueTask IServerSubscription.AddGenericHandler(Func<object?, Task> handler)
        {
            async Task WrapHandler (object? value) => await handler.Invoke((T?)value!);

            if (this is IServerSubscription<T> clientSubscription && !clientSubscription.Handlers.TryGetValue(handler, out var _))
            {
                clientSubscription.Handlers[handler] = WrapHandler;
                OnEventReceived += WrapHandler;
            }
        }

        async ValueTask IServerSubscription<T>.RemoveHandler(Func<T, Task> handler)
        {
            if (this is IServerSubscription<T> clientSubscription && clientSubscription.Handlers.TryRemove(handler, out var removedHandler))
            {
                OnEventReceived -= removedHandler;
            }
        }

        async ValueTask IServerSubscription.RemoveGenericHandler(Func<object?, Task> handler)
        {
            if (this is IServerSubscription<T> clientSubscription && clientSubscription.Handlers.TryRemove(handler, out var removedHandler))
            {
                OnEventReceived -= removedHandler;
            }
        }

        async ValueTask IServerSubscription<T>.Emit(T? eventValue)
        {
            OnEventReceived?.Invoke(eventValue);
        }

        async ValueTask IServerSubscription.EmitGeneric(object? eventValue)
        {
            if(OnEventReceived != null)
                await OnEventReceived.Invoke((T?)eventValue);
        }
    }
}