using System.Collections.Concurrent;
using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Core.Configuration
{
    /// <summary>
    /// The implementation of the core server options.
    /// </summary>
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
        private readonly ConcurrentDictionary<string, object?> _externalSettings = new();


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
        private TokenValidationParameters? tokenValidationParameters;

        /// <inheritdoc/>
        public int MaxWebSocketMessageSize => maxWsSize ?? (64 * 1024); // 64 KB

        /// <inheritdoc/>
        public int MaxHttpMessageSize => maxHttpSize ?? (128 * 1024);   // 128 KB

        /// <inheritdoc/> 
        public TimeSpan WebSocketTimeout => wsTimeout ?? TimeSpan.FromSeconds(30);

        /// <inheritdoc/> 
        public TimeSpan HttpTimeout => httpTimeout ?? TimeSpan.FromSeconds(15);

        /// <inheritdoc/> 
        public string WebSocketPathPrefix => wsPrefix ?? "/ws";

        /// <inheritdoc/> 
        public string HttpPathPrefix => httpPrefix ?? "";

        /// <inheritdoc/>
        public bool WebSocketIngestIsAllowed => allowWsIngest ?? true;

        /// <inheritdoc/>
        public bool WebSocketSubscriptionIsAllowed => allowWsSubs ?? true;

        /// <inheritdoc/>
        public bool WebSocketStreamIsAllowed => webSocketStreamIsAllowed ?? true;

        /// <inheritdoc/>
        public bool WebSocketMethodsIsAllowed => allowWsMethods ?? true;

        /// <inheritdoc/>
        public bool WebsocketRequiresPing => websocketRequiresPing ?? true;

        /// <inheritdoc/>
        public bool WebSocketPongEnabled => pongEnabled ?? true;

        /// <inheritdoc/>
        public bool MessageRetryIsEnabled => messageRetryIsEnabled ?? false;

        /// <inheritdoc/>
        public bool DetailedErrorsEnabled => detailedErrorsEnabled ?? false;

        /// <inheritdoc/>
        public Action<IEndpointConventionBuilder>? EndpointConventions => endpointConventions;

        /// <inheritdoc/>
        public Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig => routeHandlerBuilderConfig;

        /// <inheritdoc/>
        public bool ThrottlingIsDisabled => throttlingIsDisabled ?? false;

        /// <inheritdoc/>
        public bool WebsocketRequiresAuthorization => requiresAuthorization ?? true;

        /// <inheritdoc/>
        public bool WebsocketLoggingEnabled => websocketLoggingEnabled ?? false;

        /// <inheritdoc/>
        public bool HttpLoggingEnabled => httpLoggingEnabled ?? false;

        /// <inheritdoc/>
        public TimeSpan IngestTimeout => ingestTimeout ?? TimeSpan.FromSeconds(30);

        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketReaderRateLimiter => websocketReaderRateLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketPingRateLimiter => websocketPingRateLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? HttpRoundTripMethodRateLimiter => httpRoundTripMethodRateLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? HttpFireAndForgetMethodLimiter => httpFireAndForgetMethodLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketRoundTripMethodRateLimiter => websocketRoundTripMethodRateLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketFireAndForgetMethodLimiter => websocketFireAndForgetMethodLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketIngestRateLimiter => websocketIngestRateLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketSubscriptionRateLimiter => websocketSubscriptionRateLimiter;
        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketStreamingRateLimiter => websocketStreamingRateLimiter;

        /// <inheritdoc/>
        public bool RemoteCancellationIsAllowed => remoteCancellationIsAllowed ?? false;

        /// <inheritdoc/>
        public Func<TokenBucketRateLimiterOptions>? WebsocketTokenUpdateRateLimiter => websocketTokenUpdateRateLimiter;

        /// <inheritdoc/>
        public bool CheckTokenExpirationOnMsgReceived => checkTokenExpirationOnMsgReceived ?? true;

        /// <inheritdoc/>
        public bool MethodOverloadingIsEnabled => methodOverloadingIsEnabled ?? false;

        /// <inheritdoc/>
        public int MaxConcurrentOperations => maxConcurrentOperations ?? 0;

        /// <inheritdoc/>
        public IReadOnlyDictionary<Type, HubconTransportAttribute> DefaultTransports => defaultTransportAttributes;

        /// <inheritdoc/>
        public IReadOnlyDictionary<HubconTransportAttribute, Type> AuthHandlerTypes => _authHandlerTypes;

        public IReadOnlyDictionary<string, object?> ExternalSettings => _externalSettings;

        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions GlobalRateLimiterOptions => _globalRateLimiterOptions ?? new TokenBucketRateLimiterOptions()
        {
            AutoReplenishment = true,
            QueueLimit = 5000,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokenLimit = 999999999,
            TokensPerPeriod = 999999999
        };

        /// <inheritdoc/>
        public TokenValidationParameters? TokenValidationParameters => tokenValidationParameters;

        /// <inheritdoc/>
        public ICoreServerOptions SetMaxWebSocketMessageSize(int bytes)
        {
            maxWsSize ??= bytes;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetMaxHttpMessageSize(int bytes)
        {
            maxHttpSize ??= bytes;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetWebSocketTimeout(TimeSpan timeout)
        {
            wsTimeout ??= timeout;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetHttpTimeout(TimeSpan timeout)
        {
            httpTimeout ??= timeout;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableWebSocketPong(bool disabled = true)
        {
            pongEnabled ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetWebSocketPathPrefix(string prefix)
        {
            wsPrefix ??= "/" + prefix;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetHttpPathPrefix(string prefix)
        {
            httpPrefix ??= prefix;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableWebSocketIngest(bool disabled = true)
        {
            allowWsIngest ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableWebSocketStream(bool disabled = true)
        {
            webSocketStreamIsAllowed ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableWebSocketSubscriptions(bool disabled = true)
        {
            allowWsSubs ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableWebSocketMethods(bool disabled = true)
        {
            allowWsMethods ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableWebsocketPing(bool disabled = true)
        {
            websocketRequiresPing ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisabledRetryableMessages(bool disabled = true)
        {
            messageRetryIsEnabled ??= !disabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true)
        {
            detailedErrorsEnabled ??= enabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure)
        {
            endpointConventions ??= configure;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure)
        {
            routeHandlerBuilderConfig ??= configure;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableAllRateLimiters()
        {
            throttlingIsDisabled ??= true;
            return this;
        }


        /// <inheritdoc/>
        public ICoreServerOptions EnableWebsocketsLogging(bool enabled = true)
        {
            websocketLoggingEnabled ??= enabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions EnableHttpLogging(bool enabled = true)
        {
            httpLoggingEnabled ??= enabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetWebSocketIngestTimeout(TimeSpan timeout)
        {
            ingestTimeout ??= timeout;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions LimitWebsocketIngest(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketIngestRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions LimitWebsocketRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketRoundTripMethodRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions LimitHttpRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketRoundTripMethodRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AllowRemoteTokenCancellation()
        {
            remoteCancellationIsAllowed = true;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions LimitWebsocketSubscription(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketSubscriptionRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions LimitWebsocketStreaming(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketStreamingRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions ConfigureWebsocketRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketReaderRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions ConfigureWebsocketPingRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketPingRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions ConfigureWebsocketTokenUpdateRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory)
        {
            websocketTokenUpdateRateLimiter = rateLimiterOptionsFactory;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableTokenExpirationCheckOnWSMessage()
        {
            checkTokenExpirationOnMsgReceived = true;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions EnableEndpointOverloading()
        {
            throw new NotImplementedException("This feature is not yet implemented.");
            methodOverloadingIsEnabled = true;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetMaxConcurrentOperations(int count)
        {
            maxConcurrentOperations ??= count;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AddTransport<T>() where T : HubconTransportAttribute, new()
        {
            defaultTransportAttributes.TryAdd(typeof(T), new T());
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AddTransport<T>(T transportAttribute) where T : HubconTransportAttribute
        {
            defaultTransportAttributes.TryAdd(typeof(T), transportAttribute);
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetGlobalRateLimiter(TokenBucketRateLimiterOptions options)
        {
            _globalRateLimiterOptions ??= options;
            return this;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public ICoreServerOptions AddTransportAuth<TTransportAttribute, TAuthHandler>()
            where TTransportAttribute : HubconTransportAttribute, new()
            where TAuthHandler : class, IAuthHandler
        {
            _authHandlerTypes.TryAdd(HubconTransportAttribute.GetDefault<TTransportAttribute>(), typeof(TAuthHandler));
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AllowAnonymousWebSocketClients()
        {
            this.requiresAuthorization ??= false;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetTokenValidationParameters(TokenValidationParameters tokenValidationParameters)
        {
            this.tokenValidationParameters ??= tokenValidationParameters;
            return this;
        }

        
        /// <inheritdoc/>
        public ICoreServerOptions AddSetting(string key, object? value)
        {
            if(_externalSettings.TryGetValue(key, out var result))
            {
                _externalSettings.TryUpdate(key, value, result);
            }
            else
            {
                _externalSettings.TryAdd(key, value);
            }

            return this;
        }
    }
}