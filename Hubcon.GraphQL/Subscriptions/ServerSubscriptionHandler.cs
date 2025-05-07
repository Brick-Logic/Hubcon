using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System.Reflection;

namespace Hubcon.GraphQL.Subscriptions
{
    public class ServerSubscriptionHandler : ISubscription
    {
        public PropertyInfo Property { get; } = null!;


        private SubscriptionState _connected = SubscriptionState.Emitter;
        public SubscriptionState Connected => _connected;
        

        public event HubconEventHandler? OnEventReceived;

        public ServerSubscriptionHandler()
        {
        }

        public void AddHandler(HubconEventHandler handler)
        {
            OnEventReceived += handler;
        }

        public void RemoveHandler(HubconEventHandler handler)
        {
            OnEventReceived -= handler;
        }

        public async Task Subscribe()
        {
            await Task.CompletedTask;
        }

        public async Task Unsubscribe()
        {
            await Task.CompletedTask;
        }

        public void Build()
        {
        }

        public void Emit(object? eventValue)
        {
            OnEventReceived?.Invoke(eventValue);
        }
    }
}
