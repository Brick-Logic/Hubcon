using Castle.Core.Internal;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.GraphQL.Subscriptions
{
    public class ClientSubscriptionHandler : ISubscription
    {
        public event HubconEventHandler? OnEventReceived;
        private readonly IHubconGraphQLClient _client;
        private readonly IDynamicConverter _converter;
        private CancellationTokenSource _tokenSource;


        private bool _isSubscribed = false;
        public bool IsSubscribed { get => _isSubscribed; }

        public PropertyInfo Property { get; } = null!;

        public ClientSubscriptionHandler(IHubconGraphQLClient client, IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;
            _tokenSource = new CancellationTokenSource();
        }

        public void AddHandler(HubconEventHandler handler)
        {
            OnEventReceived += handler;
        }

        public void RemoveHandler(HubconEventHandler handler)
        {
            OnEventReceived -= handler;
        }

        public void Subscribe()
        {
            if(_tokenSource.IsCancellationRequested == false && _isSubscribed == true)
                return;

            _tokenSource = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                IAsyncEnumerable<JsonElement> eventSource = null!;

                var contract = Property.ReflectedType!.GetInterfaces().Find(x => x.IsAssignableTo(typeof(IControllerContract)));
                var request = new SubscriptionRequest(Property.Name, contract.Name);

                eventSource = _client.GetSubscription(request, nameof(IHubconEntrypoint.HandleSubscription), _tokenSource.Token);
                await using var enumerator = eventSource.GetAsyncEnumerator(_tokenSource.Token);
                _isSubscribed = true;

                while (await enumerator.MoveNextAsync())
                {
                    var result = _converter.DeserializeJsonElement<object>(enumerator.Current);
                    OnEventReceived?.Invoke(result);
                };

                _isSubscribed = false;
            });
        }

        public void Unsubscribe()
        {
            _tokenSource.Cancel();      
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
