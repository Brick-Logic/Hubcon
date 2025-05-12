using Castle.Core.Internal;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.GraphQL.Models;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.GraphQL.Subscriptions
{
    public class ClientSubscriptionHandler<T> : ISubscription<T>
    {
        public event HubconEventHandler<object>? OnEventReceived;
        private readonly IHubconClient _client;
        private readonly IDynamicConverter _converter;
        private CancellationTokenSource _tokenSource;


        private SubscriptionState _connected = SubscriptionState.Disconnected;
        public SubscriptionState Connected { get => _connected; }

        public PropertyInfo Property { get; } = null!;

        public Dictionary<object, HubconEventHandler<object>> Handlers { get; }

        public ClientSubscriptionHandler(IHubconClient client, IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;
            _tokenSource = new CancellationTokenSource();
            Handlers = new(); 
        }

        public void AddHandler(HubconEventHandler<T> handler)
        {
            HubconEventHandler<object> internalHandler = async (value) => await handler.Invoke((T?)value!);
            Handlers[handler] = internalHandler;
            OnEventReceived += internalHandler;
        }

        public void AddGenericHandler(HubconEventHandler<object> handler)
        {
            HubconEventHandler<object> internalHandler = async (object? value) => await handler.Invoke((T?)value!);
            Handlers[handler] = internalHandler;
            OnEventReceived += internalHandler;
        }

        public void RemoveHandler(HubconEventHandler<T> handler)
        {
            var internalHandler = Handlers[handler];
            OnEventReceived -= internalHandler;
            Handlers.Remove(handler);
        }

        public void RemoveGenericHandler(HubconEventHandler<object> handler)
        {
            var internalHandler = Handlers[handler];
            OnEventReceived -= internalHandler;
            Handlers.Remove(handler);
        }

        public async Task Subscribe()
        {
            if (_tokenSource.IsCancellationRequested == false && (_connected == SubscriptionState.Connected || _connected == SubscriptionState.Reconnecting))
                return;

            _tokenSource = new CancellationTokenSource();

            var tcs = new TaskCompletionSource();

            _ = Task.Run(async () =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    try
                    {
                        IAsyncEnumerable<JsonElement> eventSource = null!;

                        var contract = Property.ReflectedType!.GetInterfaces().Find(x => x.IsAssignableTo(typeof(IControllerContract)));
                        var request = new SubscriptionRequest(Property.Name, contract.Name);

                        eventSource = _client.GetSubscription(request, nameof(IHubconEntrypoint.HandleSubscription), _tokenSource.Token);
                        await using var enumerator = eventSource.GetAsyncEnumerator(_tokenSource.Token);
                        _connected = SubscriptionState.Connected;

                        if (tcs.Task.IsCompleted == false)
                        {
                            Console.WriteLine("Subscription connected.");
                            tcs.SetResult();
                        }
                        else
                        {
                            Console.WriteLine("Subscription reconnected.");
                        }

                        while (await enumerator.MoveNextAsync())
                        {
                            var result = _converter.DeserializeJsonElement<T>(enumerator.Current);
                            OnEventReceived?.Invoke(result);
                        };
                    }
                    catch (Exception ex)
                    {
                        _connected = SubscriptionState.Reconnecting;
                        Console.WriteLine(ex);
                        Console.WriteLine("Reconnecting...");
                    }
                }
                _connected = SubscriptionState.Disconnected;
            });

            await tcs.Task;
        }

        public async Task Unsubscribe()
        {
            while (_connected == SubscriptionState.Connected || _connected == SubscriptionState.Reconnecting)
            {
                await Task.Delay(100);
            }
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