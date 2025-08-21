using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientOptions
    {
        public Uri? BaseUri { get; }
        public List<Type> Contracts { get; }
        public Type? AuthenticationManagerType { get; }
        public string? HttpPrefix { get; }
        public string? WebsocketPrefix { get; }
        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; }
        public Action<HttpClient, IServiceProvider>? HttpClientOptions { get; }
        public bool UseSecureConnection { get; }
        TimeSpan WebsocketPingInterval { get; }
        bool WebsocketRequiresPong { get; }
        int MessageProcessorsCount { get; }
        bool AutoReconnect { get; }
        bool ReconnectStreams { get; }
        bool ReconnectSubscriptions { get; }
        bool ReconnectIngests { get; }
        TimeSpan WebsocketTimeout { get; }
        TimeSpan HttpTimeout { get; }
        RateLimiter? RateBucket { get; }
        TokenBucketRateLimiterOptions? RateBucketOptions { get; }
        bool LimitersDisabled { get; }
        public string ServerModuleName  { get; }

        public TokenBucketRateLimiterOptions? IngestLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? SubscriptionLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? StreamingLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? WebsocketRoundTripLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? HttpRoundTripLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? WebsocketFireAndForgetLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? HttpFireAndForgetLimiterOptions { get; }

        RateLimiter? IngestRateBucket { get; }
        RateLimiter? SubscriptionRateBucket { get; }
        RateLimiter? StreamingRateBucket { get; }
        RateLimiter? WebsocketRoundTripRateBucket { get; }
        RateLimiter? HttpRoundTripRateBucket { get; }
        RateLimiter? WebsocketFireAndForgetRateBucket { get; }
        RateLimiter? HttpFireAndForgetRateBucket { get; }
    }
}
