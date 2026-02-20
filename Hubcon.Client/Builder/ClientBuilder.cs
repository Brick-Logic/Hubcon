using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Proxies;
using Hubcon.Client.Core.Subscriptions;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Lazy;
using Hubcon.Shared.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Builder
{
    internal sealed class ClientBuilder : IClientBuilder, IClientOptions
    {
        public Uri? BaseUri { get; set; }
        public List<Type> Contracts { get; set; } = new List<Type>();
        public Type? AuthenticationManagerType { get; set; }
        public ILazyWrapper AuthenticationManagerFactory { get; set; }
        public string? HttpPrefix { get; set; }
        public string? WebsocketPrefix { get; set; }
        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }
        public Action<HttpClient, IServiceProvider>? HttpClientOptions { get; set; }
        public bool UseSecureConnection { get; set; } = true;
        public TimeSpan WebsocketPingInterval { get; set; } = TimeSpan.FromSeconds(15);
        public bool WebsocketRequiresPong { get; set; } = true;
        public int MessageProcessorsCount { get; set; } = 1;
        public TimeSpan WebsocketTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public string ServerModuleName { get; }

        private ConcurrentDictionary<Type, IContractOptions> _contractOptions { get; } = new ConcurrentDictionary<Type, IContractOptions>();
        private ConcurrentDictionary<InterceptorType, Func<IInvocationContext, Task>> _interceptors = new ConcurrentDictionary<InterceptorType, Func<IInvocationContext, Task>>();
        private readonly ConcurrentDictionary<Type, object> _clients = new ConcurrentDictionary<Type, object>();
        public bool AutoReconnect { get; set; } = true;
        public bool ReconnectStreams { get; set; } = false;
        public bool ReconnectSubscriptions { get; set; } = true;
        public bool ReconnectIngests { get; set; } = false;

        private RateLimiter? _rateBucket;
        public RateLimiter? RateBucket => _rateBucket ??= RateBucketOptions != null ? new TokenBucketRateLimiter(RateBucketOptions) : null;

        public TokenBucketRateLimiterOptions? RateBucketOptions { get; set; }
        public bool LimitersDisabled { get; set; }

        public bool UseHttpEndpointOverloading { get; set; }

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
        private readonly IProxyRegistry proxyRegistry;

        public ClientBuilder(IProxyRegistry proxyRegistry, string name)
        {
            this.proxyRegistry = proxyRegistry;
            ServerModuleName = name;
        }

        public RateLimiter? HttpFireAndForgetRateBucket => _httpFireAndForgetRateBucket ??= HttpFireAndForgetLimiterOptions != null ? new TokenBucketRateLimiter(HttpFireAndForgetLimiterOptions) : null;

        public bool LoggingEnabled { get; set; }
        public bool DebuggingMethodSignaturesEnabled { get; set; }
        public bool AuthIsEnabled { get; set; } = true;

        public Func<IServiceProvider, HttpClient> HttpClientFactory { get; set; } = x => new HttpClient();

        public HubconTransportAttribute TransportType { get; set; } = HubconTransportAttribute.GetDefault<HttpTransport>();

        public Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; } = new();

        public T GetOrCreateClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(IServiceProvider services, bool useCached = true) where T : IControllerContract
        {
            return (T)GetOrCreateClient(typeof(T), services);
        }

        public object GetOrCreateClient([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type contractType, IServiceProvider services, bool useCached = true)
        {
            if (useCached && _clients.ContainsKey(contractType) && _clients.TryGetValue(contractType, out object? client))
                return client!;

            if (!Contracts.Any(x => x == contractType))
                return null!;

            var contractScope = services.CreateScope();
            var scopedServices = contractScope.ServiceProvider;

            var proxyType = proxyRegistry.TryGetProxy(contractType);

            var hubconClient = scopedServices.GetService<IHubconClient>();

            var newClient = (BaseContractProxy)scopedServices.GetRequiredService(proxyType);
            var converter = scopedServices.GetRequiredService<IDynamicConverter>();

            IImmutableDictionary<string, IClientOperationContext> operations = null!;
   

            static IEnumerable<PropertyInfo> CheckType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type x)
                => x.GetProperties().Where(CheckProperty);

            static bool CheckProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] PropertyInfo x)
                => typeof(ISubscription).IsAssignableFrom(x.PropertyType);

            Func<Type, IEnumerable<PropertyInfo>> typeGetter = CheckType;

            if (useCached) _clients.TryAdd(contractType, newClient!);

            operations = newClient.BuildContractProxy(hubconClient!, this, contractScope, _contractOptions, converter);

            return newClient!;
        }

        public void UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IServiceCollection services) where T : class, IAuthenticationManager
        {
            if (AuthenticationManagerType != null)
                return;

            AuthenticationManagerType = typeof(T);

            if (!typeof(BaseAuthenticationManager).IsAssignableFrom(AuthenticationManagerType) || AuthenticationManagerType == typeof(BaseAuthenticationManager))
                throw new ArgumentException($"The provided authentication manager ('{AuthenticationManagerType.FullName}') does not derive from the {nameof(BaseAuthenticationManager)} class and cannot be registered.");

            AuthenticationManagerFactory = new LazyWrapper<T>();

            HubconClientBuilder.Current.Services.AddSingleton<T>();
        }

        public void UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(IServiceCollection services, TimeSpan refreshBeforeExpirationMargin, TimeSpan refreshCheckInterval) where T : class, IAuthenticationManager
        {
            if (AuthenticationManagerType != null)
                return;

            AuthenticationManagerType = typeof(T);

            if (!typeof(BaseAuthenticationManager).IsAssignableFrom(AuthenticationManagerType) || AuthenticationManagerType == typeof(BaseAuthenticationManager))
                throw new ArgumentException($"The provided authentication manager ('{AuthenticationManagerType.FullName}') does not derive from the {nameof(BaseAuthenticationManager)} class and cannot be registered.");

            AuthenticationManagerFactory = new LazyWrapper<T>(x =>
            {
                if(x is IBuildableAuthenticationManager buildableAuthenticationManager)
                {
                    buildableAuthenticationManager.Build(refreshBeforeExpirationMargin, refreshCheckInterval);
                }
            });

            HubconClientBuilder.Current.Services.AddSingleton<T>();
        }

        public void LoadContractProxy([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type contractType, IServiceCollection services)
        {
            HubconClientBuilder.Current.LoadContractProxy(contractType, services);
        }

        public void UseHttpClientFactory(Func<IServiceProvider, HttpClient> httpClientFactory)
        {
            HttpClientFactory = httpClientFactory;
        }

        public void ConfigureContract<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(Action<IContractConfigurator<T>>? configure) where T : IControllerContract
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

        public IContractOptions GetContractOptions(Type type)
        {
            return _contractOptions.GetOrAdd(type, contractType =>
            {
                var openType = typeof(ContractOptions<>);
                var closedType = openType.MakeGenericType(contractType);
                return (IContractOptions)Activator.CreateInstance(closedType)!;
            });
        }

        public void AddInterceptor(InterceptorType interceptorType, Func<IInvocationContext, Task> interceptorDelegate)
        {
            _interceptors.TryAdd(interceptorType, interceptorDelegate);
        }

        public Task CallInterceptor(InterceptorType interceptorType, IInvocationContext context)
        {
            return _interceptors.GetOrAdd(interceptorType, _ => Task.CompletedTask).Invoke(context);
        }

        public void EnableHttpEndpointOverloading()
        {
            UseHttpEndpointOverloading = true;
        }

    }

    public static class SubscriptionFactory
    {
        private static Func<Type, object, object>? _factory;

        public static Func<Type, object, object>? GetFactory()
        {
            return _factory!;
        }

        public static void SetupSubscriptionFactory(Func<Type, object, object> factory)
        {
            _factory ??= factory;
        }
    }
}