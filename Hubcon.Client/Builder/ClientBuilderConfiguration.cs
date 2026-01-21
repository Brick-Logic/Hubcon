using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.RateLimiting;

namespace Hubcon.Client.Builder
{
    internal class ServerModuleConfiguration : IServerModuleConfiguration
    {
        private readonly IClientBuilder builder;
        private readonly IServiceCollection services;

        public ServerModuleConfiguration(IClientBuilder builder, IServiceCollection services)
        {
            this.builder = builder;
            this.services = services;
        }

        public IServerModuleConfiguration Implements<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(Action<IContractConfigurator<T>>? configure = null) where T : IControllerContract
        {
            var type = typeof(T);

            if (type.IsClass || builder.Contracts.Any(x => x == type))
                return this;

            LoadContractProxy(type);
            builder.Contracts.Add(type);
            builder.ConfigureContract(configure);

            return this;
        }

        private void LoadContractProxy(Type contractType)
        {
            builder.LoadContractProxy(contractType, services);
        }

        public IServerModuleConfiguration UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IAuthenticationManager
        {
            builder.UseAuthenticationManager<T>(services);
            return this;
        }

        public IServerModuleConfiguration WithBaseUrl(string hostUrl)
        {
            builder.BaseUri ??= new Uri(hostUrl);
            return this;
        }

        public IServerModuleConfiguration UseInsecureConnection()
        {
            builder.UseSecureConnection = false;
            return this;
        }

        public IServerModuleConfiguration ConfigureWebsocketClient(Action<ClientWebSocketOptions, IServiceProvider> options)
        {
            builder.WebSocketOptions ??= options;
            return this;
        }

        public IServerModuleConfiguration ConfigureHttpClient(Action<HttpClient, IServiceProvider> configure)
        {
            builder.HttpClientOptions ??= configure;
            return this;
        }

        public IServerModuleConfiguration WithHttpPrefix(string prefix)
        {
            builder.HttpPrefix ??= prefix;
            return this;
        }

        public IServerModuleConfiguration WithWebsocketEndpoint(string endpoint)
        {
            builder.WebsocketPrefix ??= endpoint;
            return this;
        }

        public IServerModuleConfiguration SetWebsocketPingInterval(TimeSpan timeSpan)
        {
            builder.WebsocketPingInterval = timeSpan;
            return this;
        }

        public IServerModuleConfiguration RequirePongResponse(bool value = true)
        {
            builder.WebsocketRequiresPong = value;
            return this;
        }

        public IServerModuleConfiguration ScaleMessageProcessors(int count = 1)
        {
            builder.MessageProcessorsCount = count;
            return this;
        }

        public IServerModuleConfiguration EnableWebsocketAutoReconnect(bool value = true)
        {
            builder.AutoReconnect = value;
            return this;
        }

        public IServerModuleConfiguration ResubcribeStreamingOnReconnect(bool value = true)
        {
            builder.ReconnectStreams = value;
            return this;
        }

        public IServerModuleConfiguration ResubscribeOnReconnect(bool value = true)
        {
            builder.ReconnectSubscriptions = value;
            return this;
        }

        public IServerModuleConfiguration ResubscribeIngestOnReconnect(bool value = true)
        {
            builder.ReconnectIngests = value;
            return this;
        }

        public IServerModuleConfiguration GlobalLimit(int requestsPerSecond)
        {
            var requestsPerSec = requestsPerSecond == 0 ? 9999999 : requestsPerSecond;

            builder.RateBucketOptions = new TokenBucketRateLimiterOptions()
            {
                AutoReplenishment = true,
                QueueLimit = 9999999,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = requestsPerSec,
                TokensPerPeriod = requestsPerSec
            };

            return this;
        }

        public IServerModuleConfiguration DisableAllLimiters()
        {
            builder.LimitersDisabled = true;
            return this;
        }

        public IServerModuleConfiguration LimitIngest(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.IngestLimiterOptions = new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitSubscription(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.SubscriptionLimiterOptions = new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitStreaming(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.StreamingLimiterOptions = new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitWebsocketRoundTrip(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.WebsocketRoundTripLimiterOptions = new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitHttpRoundTrip(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.HttpRoundTripLimiterOptions = new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitWebsocketFireAndForget(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.WebsocketFireAndForgetLimiterOptions ??= new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitHttpFireAndForget(int messagesPerSecond)
        {
            var limit = messagesPerSecond == 0 ? 9999999 : messagesPerSecond;

            builder.HttpFireAndForgetLimiterOptions = new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = limit,
                TokensPerPeriod = limit
            };

            return this;
        }

        public IServerModuleConfiguration LimitIngest(TokenBucketRateLimiterOptions? options)
        {
            builder.IngestLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration LimitSubscription(TokenBucketRateLimiterOptions? options)
        {
            builder.SubscriptionLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration LimitStreaming(TokenBucketRateLimiterOptions? options)
        {
            builder.StreamingLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration LimitWebsocketRoundTrip(TokenBucketRateLimiterOptions? options)
        {
            builder.WebsocketRoundTripLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration LimitWebsocketFireAndForget(TokenBucketRateLimiterOptions? options)
        {
            builder.WebsocketFireAndForgetLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration LimitHttpRoundTrip(TokenBucketRateLimiterOptions? options)
        {
            builder.HttpRoundTripLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration LimitHttpFireAndForget(TokenBucketRateLimiterOptions? options)
        {
            builder.HttpFireAndForgetLimiterOptions = options;
            return this;
        }

        public IServerModuleConfiguration GlobalLimit(TokenBucketRateLimiterOptions? options)
        {
            builder.RateBucketOptions = options;
            return this;
        }

        public IServerModuleConfiguration EnableLogging()
        {
            builder.LoggingEnabled = true;
            return this;
        }

        public IServerModuleConfiguration DisableHttpAuthentication()
        {
            builder.HttpAuthIsEnabled = false;
            return this;
        }

        public IServerModuleConfiguration AddInterceptor(InterceptorType interceptorType, Func<InvocationContext, Task> interceptorDelegate)
        {
            builder.AddInterceptor(interceptorType, interceptorDelegate);
            return this;
        }

        public IServerModuleConfiguration EnableHttpEndpointOverloading()
        {
            throw new NotImplementedException("This feature is not yet implemented.");
            builder.EnableHttpEndpointOverloading();
            return this;
        }

        public IServerModuleConfiguration NonHubconServer()
        {
            builder.IsNonHubconServer = true;
            return this;
        }
    }
}