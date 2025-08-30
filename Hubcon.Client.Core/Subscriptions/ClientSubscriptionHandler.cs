using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Core.Subscriptions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ClientSubscriptionHandler<T> : ISubscription<T>
    {
        public event HubconEventHandler<object>? OnEventReceived;
        private readonly IDynamicConverter _converter;
        private readonly ILogger<ClientSubscriptionHandler<T>> logger;
        private CancellationTokenSource _tokenSource;


        private SubscriptionState _connected = SubscriptionState.Disconnected;
        public SubscriptionState Connected { get => _connected; }

        public PropertyInfo Property { get; } = null!;
        public IHubconClient Client { get; }

        public Dictionary<object, HubconEventHandler<object>> Handlers { get; }

        public ClientSubscriptionHandler(IDynamicConverter converter, ILogger<ClientSubscriptionHandler<T>> logger)
        {
            _converter = converter;
            this.logger = logger;
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
            HubconEventHandler<object> internalHandler = async (value) => await handler.Invoke((T?)value!);
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
                int retry = 0;

                while (!_tokenSource.IsCancellationRequested)
                {
                    try
                    {
                        IAsyncEnumerable<JsonElement> eventSource = null!;

                        var contract = Property.DeclaringType!;
                        var request = new SubscriptionRequest(Property.Name, contract.Name, null);

                        eventSource = await Client.GetSubscription(request, Property, _tokenSource.Token);
                        
                        _connected = SubscriptionState.Connected;

                        tcs.SetResult();

                        await foreach(var item in eventSource)
                        {
                            if (retry > 0) retry = 0;

                            var result = _converter.DeserializeData<T>(item);

                            if(OnEventReceived != null)
                                await OnEventReceived.Invoke(result);
                        };
                    }
                    catch (Exception ex)
                    {
                        retry += 1;
                        _connected = SubscriptionState.Reconnecting;
                        logger.LogError(ex.Message, ex);

                        int baseReconnectionDelay = 1000;
                        int maxReconnectionDelay = 3000;

                        int expDelay = baseReconnectionDelay * (int)Math.Pow(2, retry);
                        int jitter = Random.Shared.Next(0, 1000);
                        int delay = Math.Min(expDelay + jitter, maxReconnectionDelay);

                        await Task.Delay(delay);
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