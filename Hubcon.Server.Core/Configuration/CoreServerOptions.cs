using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Core.Configuration
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class CoreServerOptions : ICoreServerOptions, IInternalServerOptions
    {
        private int? maxWsSize;
        private int? maxHttpSize;
        private TimeSpan? wsTimeout;
        private TimeSpan? httpTimeout;
        private bool? pongEnabled;
        private string? wsPrefix;
        private string? httpPrefix;
        private bool? allowWsIngest;
        private bool? allowWsSubs;
        private bool? allowWsMethods;
        private bool? websocketRequiresPing;
        private bool? messageRetryIsEnabled;
        private bool? webSocketStreamIsAllowed;
        private bool? detailedErrorsEnabled;
        private Action<IEndpointConventionBuilder>? endpointConventions;
        private Action<RouteHandlerBuilder>? routeHandlerBuilderConfig;
        private bool? throttlingIsDisabled;
        private Func<string, IServiceProvider, (ClaimsPrincipal, DateTime expirationDate)?>? tokenHandler;
        private bool? requiresAuthorization;
        private bool? websocketLoggingEnabled;
        private bool? httpLoggingEnabled;
        private TimeSpan? ingestTimeout;
        private bool? remoteCancellationIsAllowed;
        private bool? checkTokenExpirationOnMsgReceived;
        private bool? methodOverloadingIsEnabled;
        private int? maxConcurrentOperations;
        private Dictionary<Type, HubconTransportAttribute> defaultTransportAttributes = new Dictionary<Type, HubconTransportAttribute>();
        private TokenBucketRateLimiterOptions? _globalRateLimiterOptions;
        private readonly Dictionary<HubconTransportAttribute, Type> _authHandlerTypes = new Dictionary<HubconTransportAttribute, Type>();


        private Func<TokenBucketRateLimiterOptions>? websocketReaderRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketPingRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? httpRoundTripMethodRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? httpFireAndForgetMethodLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketRoundTripMethodRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketFireAndForgetMethodLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketIngestRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketSubscriptionRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketStreamingRateLimiter = null;

        private Func<TokenBucketRateLimiterOptions>? websocketTokenUpdateRateLimiter = null;

        // Defaults
        public int MaxWebSocketMessageSize => maxWsSize ?? (64 * 1024); // 64 KB
        public int MaxHttpMessageSize => maxHttpSize ?? (128 * 1024);   // 128 KB

        public TimeSpan WebSocketTimeout => wsTimeout ?? TimeSpan.FromSeconds(30);
        public TimeSpan HttpTimeout => httpTimeout ?? TimeSpan.FromSeconds(15);

        public string WebSocketPathPrefix => wsPrefix ?? "/ws";
        public string HttpPathPrefix => httpPrefix ?? "";

        public bool WebSocketIngestIsAllowed => allowWsIngest ?? true;
        public bool WebSocketSubscriptionIsAllowed => allowWsSubs ?? true;
        public bool WebSocketStreamIsAllowed => webSocketStreamIsAllowed ?? true;
        public bool WebSocketMethodsIsAllowed => allowWsMethods ?? true;
        public bool WebsocketRequiresPing => websocketRequiresPing ?? true;
        public bool WebSocketPongEnabled => pongEnabled ?? true;
        public bool MessageRetryIsEnabled => messageRetryIsEnabled ?? false;
        public bool DetailedErrorsEnabled => detailedErrorsEnabled ?? false;
        public Action<IEndpointConventionBuilder>? EndpointConventions => endpointConventions;
        public Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig => routeHandlerBuilderConfig;

        public bool ThrottlingIsDisabled => throttlingIsDisabled ?? false;

        public Func<string, IServiceProvider, (ClaimsPrincipal, DateTime expirationDate)?>? TokenHandler => tokenHandler;

        public bool WebsocketRequiresAuthorization => requiresAuthorization ?? true;

        public bool WebsocketLoggingEnabled => websocketLoggingEnabled ?? false;

        public bool HttpLoggingEnabled => httpLoggingEnabled ?? false;

        public TimeSpan IngestTimeout => ingestTimeout ?? TimeSpan.FromSeconds(30);

        public Func<TokenBucketRateLimiterOptions>? WebsocketReaderRateLimiter => websocketReaderRateLimiter;
        public Func<TokenBucketRateLimiterOptions>? WebsocketPingRateLimiter => websocketPingRateLimiter;
        public Func<TokenBucketRateLimiterOptions>? HttpRoundTripMethodRateLimiter => httpRoundTripMethodRateLimiter;
        public Func<TokenBucketRateLimiterOptions>? HttpFireAndForgetMethodLimiter => httpFireAndForgetMethodLimiter;
        public Func<TokenBucketRateLimiterOptions>? WebsocketRoundTripMethodRateLimiter => websocketRoundTripMethodRateLimiter;
        public Func<TokenBucketRateLimiterOptions>? WebsocketFireAndForgetMethodLimiter => websocketFireAndForgetMethodLimiter;
        public Func<TokenBucketRateLimiterOptions>? WebsocketIngestRateLimiter => websocketIngestRateLimiter;
        public Func<TokenBucketRateLimiterOptions>? WebsocketSubscriptionRateLimiter => websocketSubscriptionRateLimiter;
        public Func<TokenBucketRateLimiterOptions>? WebsocketStreamingRateLimiter => websocketStreamingRateLimiter;

        public bool RemoteCancellationIsAllowed => remoteCancellationIsAllowed ?? false;

        public Func<TokenBucketRateLimiterOptions>? WebsocketTokenUpdateRateLimiter => websocketTokenUpdateRateLimiter;

        public bool CheckTokenExpirationOnMsgReceived => checkTokenExpirationOnMsgReceived ?? true;

        public bool MethodOverloadingIsEnabled => methodOverloadingIsEnabled ?? false;

        public int MaxConcurrentOperations => maxConcurrentOperations ?? 0;

        public IReadOnlyDictionary<Type, HubconTransportAttribute> DefaultTransports => defaultTransportAttributes;

        public IReadOnlyDictionary<HubconTransportAttribute, Type> AuthHandlerTypes => _authHandlerTypes;

        public TokenBucketRateLimiterOptions GlobalRateLimiterOptions => _globalRateLimiterOptions ?? new TokenBucketRateLimiterOptions()
        {
            AutoReplenishment = true,
            QueueLimit = 5000,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokenLimit = 999999999,
            TokensPerPeriod = 999999999
        };

        public ICoreServerOptions SetMaxWebSocketMessageSize(int bytes)
        {
            maxWsSize ??= bytes;
            return this;
        }

        public ICoreServerOptions SetMaxHttpMessageSize(int bytes)
        {
            maxHttpSize ??= bytes;
            return this;
        }

        public ICoreServerOptions SetWebSocketTimeout(TimeSpan timeout)
        {
            wsTimeout ??= timeout;
            return this;
        }

        public ICoreServerOptions SetHttpTimeout(TimeSpan timeout)
        {
            httpTimeout ??= timeout;
            return this;
        }

        public ICoreServerOptions DisableWebSocketPong(bool disabled = true)
        {
            pongEnabled ??= !disabled;
            return this;
        }

        public ICoreServerOptions SetWebSocketPathPrefix(string prefix)
        {
            wsPrefix ??= "/" + prefix;
            return this;
        }

        public ICoreServerOptions SetHttpPathPrefix(string prefix)
        {
            httpPrefix ??= prefix;
            return this;
        }

        public ICoreServerOptions DisableWebSocketIngest(bool disabled = true)
        {
            allowWsIngest ??= !disabled;
            return this;
        }

        public ICoreServerOptions DisableWebSocketStream(bool disabled = true)
        {
            webSocketStreamIsAllowed ??= !disabled;
            return this;
        }

        public ICoreServerOptions DisableWebSocketSubscriptions(bool disabled = true)
        {
            allowWsSubs ??= !disabled;
            return this;
        }

        public ICoreServerOptions DisableWebSocketMethods(bool disabled = true)
        {
            allowWsMethods ??= !disabled;
            return this;
        }

        public ICoreServerOptions DisableWebsocketPing(bool disabled = true)
        {
            websocketRequiresPing ??= !disabled;
            return this;
        }

        public ICoreServerOptions DisabledRetryableMessages(bool disabled = true)
        {
            messageRetryIsEnabled ??= !disabled;
            return this;
        }

        public ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true)
        {
            detailedErrorsEnabled ??= enabled;
            return this;
        }

        public ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure)
        {
            endpointConventions ??= configure;
            return this;
        }

        public ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure)
        {
            routeHandlerBuilderConfig ??= configure;
            return this;
        }

        public ICoreServerOptions DisableAllRateLimiters()
        {
            throttlingIsDisabled ??= true;
            return this;
        }


        public ICoreServerOptions EnableWebsocketsLogging(bool enabled = true)
        {
            websocketLoggingEnabled ??= enabled;
            return this;
        }

        public ICoreServerOptions EnableHttpLogging(bool enabled = true)
        {
            httpLoggingEnabled ??= enabled;
            return this;
        }

        public ICoreServerOptions SetWebSocketIngestTimeout(TimeSpan timeout)
        {
            ingestTimeout ??= timeout;
            return this;
        }

        public ICoreServerOptions LimitWebsocketIngest(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketIngestRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions LimitWebsocketRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketRoundTripMethodRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions LimitHttpRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketRoundTripMethodRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions AllowRemoteTokenCancellation()
        {
            remoteCancellationIsAllowed = true;
            return this;
        }

        public ICoreServerOptions LimitWebsocketSubscription(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketSubscriptionRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions LimitWebsocketStreaming(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketStreamingRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions ConfigureWebsocketRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketReaderRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions ConfigureWebsocketPingRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketPingRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions ConfigureWebsocketTokenUpdateRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketTokenUpdateRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        public ICoreServerOptions DisableTokenExpirationCheckOnWSMessage()
        {
            checkTokenExpirationOnMsgReceived = true;
            return this;
        }

        public ICoreServerOptions EnableEndpointOverloading()
        {
            throw new NotImplementedException("This feature is not yet implemented.");
            methodOverloadingIsEnabled = true;
            return this;
        }

        public ICoreServerOptions SetMaxConcurrentOperations(int count)
        {
            maxConcurrentOperations ??= count;
            return this;
        }

        public ICoreServerOptions AddTransport<T>() where T : HubconTransportAttribute, new()
        {
            defaultTransportAttributes.TryAdd(typeof(T), new T());
            return this;
        }

        public ICoreServerOptions AddTransport<T>(T transportAttribute) where T : HubconTransportAttribute
        {
            defaultTransportAttributes.TryAdd(typeof(T), transportAttribute);
            return this;
        }

        public ICoreServerOptions SetGlobalRateLimiter(TokenBucketRateLimiterOptions options)
        {
            _globalRateLimiterOptions ??= options;
            return this;
        }

        public ICoreServerOptions SetGlobalRateLimiter(int requests, int millisecondsToReplenish = 1000, int queueLimit = 0, int rateTokenLimit = 0)
        {
            static int GetOrDefault(int limit, int defaultLimit)
            {
                return limit switch
                {
                    0 => defaultLimit,
                    var l => l
                };
            }

            _globalRateLimiterOptions ??= new TokenBucketRateLimiterOptions()
            {
                TokenLimit = GetOrDefault(rateTokenLimit == 0 
                    ? requests : 
                    rateTokenLimit, 999999999),

                TokensPerPeriod = GetOrDefault(requests, 999999999),

                ReplenishmentPeriod = millisecondsToReplenish == 0 
                    ? TimeSpan.FromSeconds(1) 
                    : TimeSpan.FromMilliseconds(millisecondsToReplenish),

                AutoReplenishment = true,
                QueueLimit = GetOrDefault(queueLimit, requests * 2),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            };

            return this;
        }

        public ICoreServerOptions AddTransportAuth<TTransportAttribute, TAuthHandler>()
            where TTransportAttribute : HubconTransportAttribute, new()
            where TAuthHandler : class, IAuthHandler
        {
            _authHandlerTypes.TryAdd(HubconTransportAttribute.GetDefault<TTransportAttribute>(), typeof(TAuthHandler));
            return this;
        }

        public ICoreServerOptions AllowAnonymousWebSocketClients()
        {
            this.requiresAuthorization ??= false;
            return this;
        }
    }
}