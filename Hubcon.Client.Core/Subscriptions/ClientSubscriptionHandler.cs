using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Context;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Subscriptions
{
    public interface IBuildableSubscription
    {
        void Build(IClientOperationContext context);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ClientSubscriptionHandler<T> : ISubscription<T>, IBuildableSubscription
    {
        public event HubconEventHandler<object>? OnEventReceived;
        private readonly IDynamicConverter _converter;
        private readonly ILogger<ClientSubscriptionHandler<object>> logger;
        private CancellationTokenSource _tokenSource;

        private SubscriptionState _connected = SubscriptionState.Disconnected;
        public SubscriptionState Connected { get => _connected; }

        public PropertyInfo Property { get; } = null!;
        public IHubconClient Client { get; }

        public ConcurrentDictionary<object, HubconEventHandler<object>> Handlers { get; }
        public IClientOperationContext Context { get; set; }

        public ClientSubscriptionHandler(ClientSubscriptionConfig<object> subscriptionConfig)
        {
            _converter = subscriptionConfig.Converter;
            this.logger = subscriptionConfig.Logger;
            this.Property = subscriptionConfig.Property;
            this.Client = subscriptionConfig.Client;
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
            Handlers.TryRemove(handler, out _);
        }

        public void RemoveGenericHandler(HubconEventHandler<object> handler)
        {
            var internalHandler = Handlers[handler];
            OnEventReceived -= internalHandler;
            Handlers.TryRemove(handler, out _);
        }

        public async Task Subscribe()
        {
            if (_tokenSource.IsCancellationRequested == false && (_connected == SubscriptionState.Connected || _connected == SubscriptionState.Reconnecting))
                return;

            _tokenSource = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<object>();

            _ = Task.Factory.StartNew(async () =>
            {
                int retry = 0;

                var contract = Property.DeclaringType!;
                var simpleContractName = NamingHelper.GetCleanName(Property.DeclaringType!.Name);
                var request = new SubscriptionRequest(Property.Name, simpleContractName, null);
                var random = new Random();
                var scope = Context.RootServiceProvider.CreateScope();

                while (!_tokenSource.IsCancellationRequested)
                {
                    IAsyncEnumerator<JsonElement>? enumerator = null;

                    try
                    {
                        IObservable<JsonElement> observable = null!;
                        WrappedContext.SetWrapped(true);
                        var callContext = new CallContext(scope.ServiceProvider, request, Context!.AuthenticationManagerFactory?.Invoke()!, true, _tokenSource.Token);
                        HubconContext.UseContext(callContext);

                        observable = await Client.GetSubscription(request, Context, _tokenSource.Token);
                        _connected = SubscriptionState.Connected;

                        var options = new BoundedChannelOptions(999999999);
                        var observer = AsyncObserver.Create<JsonElement>(Context.Converter, options);

                        tcs.TrySetResult(null!);

                        using (observable.Subscribe(observer))
                        {
                            enumerator = observer
                                .GetAsyncEnumerable(_tokenSource.Token)
                                .GetAsyncEnumerator();

                            while (true)
                            {
                                bool moved;
                                if (retry > 0) retry = 0;

                                try
                                {
                                    moved = await enumerator.MoveNextAsync();
                                }
                                catch (Exception)
                                {
                                    moved = false;
                                }

                                if (!moved)
                                {
                                    break;
                                }

                                var item = enumerator.Current;
                                var result = _converter.DeserializeData<T>(enumerator.Current);

                                if (OnEventReceived != null)
                                    await OnEventReceived.Invoke(result!);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message, ex);
                    }
                    finally
                    {
                        retry += 1;

                        if(enumerator != null)
                            await enumerator.DisposeAsync();

                        _connected = SubscriptionState.Reconnecting;

                        int baseReconnectionDelay = 1000;
                        int maxReconnectionDelay = 3000;

                        int expDelay = baseReconnectionDelay * (int)Math.Pow(2, retry);
                        int jitter = random.Next(0, 2000);
                        int delay = Math.Min(expDelay + jitter, maxReconnectionDelay);

                        await Task.Delay(delay);
                    }
                }
                _connected = SubscriptionState.Disconnected;
                _tokenSource.Dispose();
            },
            default,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

            await tcs.Task;
        }

        public async Task Unsubscribe()
        {
            _tokenSource.Cancel();
        }

        public void Build()
        {
        }

        public void Emit(T? eventValue)
        {
            OnEventReceived?.Invoke(eventValue!);
        }

        public void EmitGeneric(object? eventValue)
        {
            OnEventReceived?.Invoke((T?)eventValue!);
        }

        public void Build(IClientOperationContext context)
        {
            Context ??= context;
        }
    }

    public class ClientSubscriptionConfig<T>
    {
        public IDynamicConverter Converter { get; set; } = null!;
        public ILogger<ClientSubscriptionHandler<T>> Logger { get; set; } = null!;
        public PropertyInfo Property { get; set; } = null!;
        public IHubconClient Client { get; set; } = null!;
    }
}