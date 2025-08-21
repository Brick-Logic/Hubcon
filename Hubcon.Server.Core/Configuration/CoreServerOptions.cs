using Microsoft.AspNetCore.Builder;
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
        private Func<string, IServiceProvider, ClaimsPrincipal?>? websocketTokenHandler;
        private bool? websocketRequiresAuthorization;
        private bool? websocketLoggingEnabled;
        private bool? httpLoggingEnabled;
        private TimeSpan? ingestTimeout;
        private bool? remoteCancellationIsAllowed;

        private Func<TokenBucketRateLimiterOptions> websocketReaderRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 500,
            TokensPerPeriod = 500,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> websocketPingRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 5,
            TokensPerPeriod = 5,
            ReplenishmentPeriod = TimeSpan.FromSeconds(5),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> httpRoundTripMethodRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 50,
            TokensPerPeriod = 50,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> httpFireAndForgetMethodLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> websocketRoundTripMethodRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 50,
            TokensPerPeriod = 50,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> websocketFireAndForgetMethodLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> websocketIngestRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 200,
            TokensPerPeriod = 200,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> websocketSubscriptionRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 20,
            TokensPerPeriod = 20,
            ReplenishmentPeriod = TimeSpan.FromSeconds(2),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        private Func<TokenBucketRateLimiterOptions> websocketStreamingRateLimiter = () => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

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

        public Func<string, IServiceProvider, ClaimsPrincipal?>? WebsocketTokenHandler => websocketTokenHandler;

        public bool WebsocketRequiresAuthorization => websocketRequiresAuthorization ?? false;

        public bool WebsocketLoggingEnabled => websocketLoggingEnabled ?? false;

        public bool HttpLoggingEnabled => httpLoggingEnabled ?? false;

        public TimeSpan IngestTimeout => ingestTimeout ?? TimeSpan.FromSeconds(30);

        public Func<TokenBucketRateLimiterOptions> WebsocketReaderRateLimiter => websocketReaderRateLimiter;
        public Func<TokenBucketRateLimiterOptions> WebsocketPingRateLimiter => websocketPingRateLimiter;
        public Func<TokenBucketRateLimiterOptions> HttpRoundTripMethodRateLimiter => httpRoundTripMethodRateLimiter;
        public Func<TokenBucketRateLimiterOptions> HttpFireAndForgetMethodLimiter => httpFireAndForgetMethodLimiter;
        public Func<TokenBucketRateLimiterOptions> WebsocketRoundTripMethodRateLimiter => websocketRoundTripMethodRateLimiter;
        public Func<TokenBucketRateLimiterOptions> WebsocketFireAndForgetMethodLimiter => websocketFireAndForgetMethodLimiter;
        public Func<TokenBucketRateLimiterOptions> WebsocketIngestRateLimiter => websocketIngestRateLimiter;
        public Func<TokenBucketRateLimiterOptions> WebsocketSubscriptionRateLimiter => websocketSubscriptionRateLimiter;
        public Func<TokenBucketRateLimiterOptions> WebsocketStreamingRateLimiter => websocketStreamingRateLimiter;

        public bool RemoteCancellationIsAllowed => remoteCancellationIsAllowed ?? false;

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

        public ICoreServerOptions UseWebsocketTokenHandler(Func<string, IServiceProvider, ClaimsPrincipal?> tokenHandler)
        {
            websocketTokenHandler ??= tokenHandler;
            websocketRequiresAuthorization ??= true;
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
    }
}