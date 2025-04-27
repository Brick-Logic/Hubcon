using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Models.CustomAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Subscriptions
{
    public class SubscriptionHandler<T> : ISubscriptionHandler<T>
    {
        private readonly IHubconGraphQLClient _client;
        private readonly DynamicConverter _converter;
        private PropertyInfo _propertyInfo = null!;
        private CancellationTokenSource _tokenSource;

        public event HubconEventHandler<T?>? OnEventReceived;

        public SubscriptionHandler(IHubconGraphQLClient client, DynamicConverter converter)
        {
            _client = client;
            _converter = converter;
            _tokenSource = new CancellationTokenSource();
        }

        public void AddHandler(HubconEventHandler<T?> handler)
        {
            OnEventReceived += handler;
        }

        public void RemoveHandler(HubconEventHandler<T?> handler)
        {
            OnEventReceived -= handler;
        }

        public void Subscribe()
        {
            _tokenSource = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                IAsyncEnumerable<JsonElement> eventSource = null!;

                var request = new MethodInvokeRequest(_propertyInfo.Name, _propertyInfo.ReflectedType!.Name);

                eventSource = _client.GetStream(request, nameof(IHubconEntrypoint.HandleMethodStream), _tokenSource.Token);
                await using var enumerator = eventSource.GetAsyncEnumerator(_tokenSource.Token);

                while (await enumerator.MoveNextAsync())
                {
                    var result = _converter.DeserializeJsonElement<T>(enumerator.Current);
                    OnEventReceived?.Invoke(result);
                };
            });
        }

        public void Unsubscribe()
        {
            _tokenSource.Cancel();      
        }

        public void Build(PropertyInfo property)
        {
            _propertyInfo = property;
        }
    }
}
