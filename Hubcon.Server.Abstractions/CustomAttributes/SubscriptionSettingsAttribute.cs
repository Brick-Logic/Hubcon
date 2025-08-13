using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class SubscriptionSettings : HubconSettings
    {
        public int ChannelCapacity { get; private set; }
        public BoundedChannelFullMode ChannelFullMode { get; private set; }

        public override TokenBucketRateLimiter RateBucket { get; }

        public SubscriptionSettings(
           int rateTokensPerPeriod = 1000,
           int rateTokenLimit = 1000,
           int queueLimit = 1000,
           QueueProcessingOrder queueProcessingOrder = QueueProcessingOrder.OldestFirst,
           TimeSpan? rateReplenishmentPeriod = null,
           int channelCapacity = 1000,
           BoundedChannelFullMode? channelFullMode = BoundedChannelFullMode.Wait)
        {
            ChannelCapacity = channelCapacity;
            ChannelFullMode = channelFullMode ?? BoundedChannelFullMode.Wait;

            static int GetOrDefault(int limit)
            {
                return limit switch
                {
                    0 => 9_999_999,
                    var l => l
                };
            }

            RateBucket = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = GetOrDefault(rateTokenLimit),
                TokensPerPeriod = GetOrDefault(rateTokensPerPeriod),
                ReplenishmentPeriod = rateReplenishmentPeriod ?? TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = GetOrDefault(queueLimit),
                QueueProcessingOrder = queueProcessingOrder
            });
        }

        public static Func<SubscriptionSettings> Factory { get; } = () => new();
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SubscriptionSettingsAttribute : Attribute
    {
        public Func<SubscriptionSettings> Factory { get; }

        /// <summary>
        /// Attribute to configure rate limiting and queue parameters for specific websocket methods.
        /// </summary>
        /// <param name="rateTokensPerPeriod">
        /// Tokens replenished per period to limit websocket rate.  
        /// </param>
        /// <param name="rateTokenLimit">
        /// Maximum capacity of the bucket for bursts.  
        /// </param>
        /// <param name="queueLimit">
        /// Maximum number of requests in the queue waiting for tokens.  
        /// </param>
        /// <param name="queueProcessingOrder">
        /// Queue processing order (OldestFirst or NewestFirst).  
        /// </param>
        /// <param name="rateReplenishmentPeriod">
        /// Token bucket replenishment period. If null or zero, defaults to 1 second.
        /// </param>
        public SubscriptionSettingsAttribute(
           int rateTokensPerPeriod = 1000,
           int rateTokenLimit = 1000,
           QueueProcessingOrder queueProcessingOrder = QueueProcessingOrder.OldestFirst,
           int channelCapacity = 1000,
           BoundedChannelFullMode? channelFullMode = BoundedChannelFullMode.Wait,
           int millisecondsToReplenish = 1000)
        {
            var replenishmentPeriod = (millisecondsToReplenish <= 0)
                ? TimeSpan.FromSeconds(1)
                : TimeSpan.FromMilliseconds(millisecondsToReplenish);

            Factory = () => new SubscriptionSettings(
                rateTokensPerPeriod,
                rateTokenLimit,
                1,
                queueProcessingOrder,
                replenishmentPeriod,
                channelCapacity,
                channelFullMode
            );
        }

        public static Func<SubscriptionSettingsAttribute> Default { get; } = () => new();
    }
}
