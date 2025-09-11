using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Proxies;
using Hubcon.Client.Core.Subscriptions;
using Hubcon.Client.Interceptors;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading.RateLimiting;


namespace Hubcon.Client.Builder
{
    internal sealed class ClientBuilder(IProxyRegistry proxyRegistry, string name) : IClientBuilder, IClientOptions
    {
        public Uri? BaseUri { get; set; }
        public List<Type> Contracts { get; set; } = new();
        public Type? AuthenticationManagerType { get; set; }
        public string? HttpPrefix { get; set; }
        public string? WebsocketPrefix { get; set; }
        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }
        public Action<HttpClient, IServiceProvider>? HttpClientOptions { get; set; }
        public bool UseSecureConnection { get; set; } = true;
        public TimeSpan WebsocketPingInterval { get; set; } = TimeSpan.FromSeconds(5);
        public bool WebsocketRequiresPong { get; set; } = true;
        public int MessageProcessorsCount { get; set; } = 1;
        public TimeSpan WebsocketTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public string ServerModuleName { get; } = name;

        private ConcurrentDictionary<Type, Type> _subTypesCache { get; } = new();
        private ConcurrentDictionary<Type, IEnumerable<PropertyInfo>> _propTypesCache { get; } = new();
        private ConcurrentDictionary<Type, IContractOptions> _contractOptions { get; } = new();
        private Dictionary<Type, object> _clients { get; } = new();
        public bool AutoReconnect { get; set; } = true;
        public bool ReconnectStreams { get; set; } = false;
        public bool ReconnectSubscriptions { get; set; } = true;
        public bool ReconnectIngests { get; set; } = false;

        private RateLimiter? _rateBucket;
        public RateLimiter? RateBucket => _rateBucket ??= RateBucketOptions != null ? new TokenBucketRateLimiter(RateBucketOptions) : null;

        public TokenBucketRateLimiterOptions? RateBucketOptions { get; set; }
        public bool LimitersDisabled { get; set; }

        public TokenBucketRateLimiterOptions? IngestLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 200,
            TokensPerPeriod = 200,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        public TokenBucketRateLimiterOptions? SubscriptionLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 20,
            TokensPerPeriod = 20,
            ReplenishmentPeriod = TimeSpan.FromSeconds(2),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        public TokenBucketRateLimiterOptions? StreamingLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        public TokenBucketRateLimiterOptions? WebsocketRoundTripLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 50,
            TokensPerPeriod = 50,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        public TokenBucketRateLimiterOptions? HttpRoundTripLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 50,
            TokensPerPeriod = 50,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        public TokenBucketRateLimiterOptions? WebsocketFireAndForgetLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        public TokenBucketRateLimiterOptions? HttpFireAndForgetLimiterOptions { get; set; } = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };



        private RateLimiter? _ingestRateBucket;
        public RateLimiter? IngestRateBucket => _ingestRateBucket ??= IngestLimiterOptions != null ? new TokenBucketRateLimiter(IngestLimiterOptions) : null;

        private RateLimiter? _subscriptionRateBucket;
        public RateLimiter? SubscriptionRateBucket => _subscriptionRateBucket ??= SubscriptionLimiterOptions != null ? new TokenBucketRateLimiter(SubscriptionLimiterOptions) : null;

        private RateLimiter? _streamingRateBucket;
        public RateLimiter? StreamingRateBucket => _streamingRateBucket ??= StreamingLimiterOptions != null ? new TokenBucketRateLimiter(StreamingLimiterOptions) : null;

        private RateLimiter? _websocketRoundTripRateBucket;
        public RateLimiter? WebsocketRoundTripRateBucket => _websocketRoundTripRateBucket ??= WebsocketRoundTripLimiterOptions != null ? new TokenBucketRateLimiter(WebsocketRoundTripLimiterOptions) : null;

        private RateLimiter? _httpRoundTripRateBucket;
        public RateLimiter? HttpRoundTripRateBucket => _httpRoundTripRateBucket ??= HttpRoundTripLimiterOptions != null ? new TokenBucketRateLimiter(HttpRoundTripLimiterOptions) : null;

        private RateLimiter? _websocketFireAndForgetRateBucket;
        public RateLimiter? WebsocketFireAndForgetRateBucket => _websocketFireAndForgetRateBucket ??= WebsocketFireAndForgetLimiterOptions != null ? new TokenBucketRateLimiter(WebsocketFireAndForgetLimiterOptions) : null;

        private RateLimiter? _httpFireAndForgetRateBucket;
        public RateLimiter? HttpFireAndForgetRateBucket => _httpFireAndForgetRateBucket ??= HttpFireAndForgetLimiterOptions != null ? new TokenBucketRateLimiter(HttpFireAndForgetLimiterOptions) : null;

        public bool LoggingEnabled {  get; set; }
        public bool DebuggingMethodSignaturesEnabled { get; set; }
        public bool HttpAuthIsEnabled { get; set; } = true;

        public T GetOrCreateClient<T>(IServiceProvider services, bool useCached = true) where T : IControllerContract
        {
            return (T)GetOrCreateClient(typeof(T), services);
        }

        public object GetOrCreateClient(Type contractType, IServiceProvider services, bool useCached = true)
        {
            if (useCached && _clients.ContainsKey(contractType) && _clients.TryGetValue(contractType, out object? client))
                return client!;

            if (!Contracts.Any(x => x == contractType))
                return default!;

            var proxyType = proxyRegistry.TryGetProxy(contractType);

            var hubconClient = services.GetService<IHubconClient>();

            hubconClient?.Build(this, services, _contractOptions, UseSecureConnection);

            var newClient = (BaseContractProxy)services.GetRequiredService(proxyType);
            var converter = services.GetRequiredService<IDynamicConverter>();

            newClient.BuildContractProxy(hubconClient!, converter);

            var props = _propTypesCache.GetOrAdd(
                proxyType,
                x => x.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))));

            foreach (var subscriptionProp in props)
            {
                var value = subscriptionProp.GetValue(newClient, null);
                if (value == null)
                {
                    var genericType = _subTypesCache.GetOrAdd(
                        subscriptionProp.PropertyType.GenericTypeArguments[0], 
                        x => typeof(ClientSubscriptionHandler<>).MakeGenericType(x));

                    var propss = contractType.GetProperties().Where(x => x.Name == subscriptionProp.Name).FirstOrDefault();

                    var subscriptionInstance = (ISubscription)services.GetRequiredService(genericType);
                    PropertyTools.AssignProperty(newClient, subscriptionProp, subscriptionInstance);
                    PropertyTools.AssignProperty(
                        subscriptionInstance,
                        nameof(ClientSubscriptionHandler<object>.Property),
                        propss
                        );
                    PropertyTools.AssignProperty(subscriptionInstance, nameof(ClientSubscriptionHandler<object>.Client), hubconClient);
                    subscriptionInstance.Build();
                }
            }

            if(useCached) _clients.Add(contractType, newClient!);

            return newClient!;
        }

        public void UseAuthenticationManager<T>(IServiceCollection services) where T : class, IAuthenticationManager
        {
            if (AuthenticationManagerType != null)
                return;

            AuthenticationManagerType = typeof(T);

            HubconClientBuilder.Current.Services.AddSingleton<T>();
        }

        public void LoadContractProxy(Type contractType, IServiceCollection services)
        {
            HubconClientBuilder.Current.LoadContractProxy(contractType, services);
        }

        public void ConfigureContract<T>(Action<IContractConfigurator<T>>? configure) where T : IControllerContract
        {
            if (configure == null)
                return;

            if (!_contractOptions.TryGetValue(typeof(T), out _))
            {
                var options = new ContractOptions<T>();
                configure(options);
                _contractOptions.TryAdd(typeof(T), options);
            }
        }
    }
}