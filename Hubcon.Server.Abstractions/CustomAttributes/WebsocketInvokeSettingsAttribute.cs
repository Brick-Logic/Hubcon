using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class WebsocketInvokeSettings : HubconSettings
    {
        public override TokenBucketRateLimiter RateBucket { get; }

        public WebsocketInvokeSettings(
            int rateTokensPerPeriod = 1000,
            int rateTokenLimit = 1000,
            int queueLimit = 1000,
            QueueProcessingOrder queueProcessingOrder = QueueProcessingOrder.OldestFirst,
            TimeSpan? rateReplenishmentPeriod = null)
        {
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

        public static Func<WebsocketCallSettings> Factory { get; } = () => new();
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WebsocketInvokeSettingsAttribute : Attribute
    {
        public Func<WebsocketInvokeSettings> Factory { get; }

        /// <summary>
        /// Attribute to configure rate limiting and queue parameters for RPC methods.
        /// </summary>
        /// <param name="rateTokensPerPeriod">
        /// Number of tokens replenished each period to limit the execution rate.
        /// </param>
        /// <param name="rateTokenLimit">
        /// Maximum capacity of the token bucket, allowing bursts up to this limit.
        /// </param>
        /// <param name="queueLimit">
        /// Maximum number of requests allowed in the queue waiting for tokens.
        /// </param>
        /// <param name="queueProcessingOrder">
        /// Order in which the queue is processed: OldestFirst or NewestFirst.
        /// </param>
        /// <param name="rateReplenishmentPeriod">
        /// Token bucket replenishment period.
        /// If null or TimeSpan.Zero, defaults to 1 second.
        /// </param>
        public WebsocketInvokeSettingsAttribute(
            int rateTokensPerPeriod = 1000,
            int rateTokenLimit = 1000,
            QueueProcessingOrder queueProcessingOrder = QueueProcessingOrder.OldestFirst,
            int millisecondsToReplenish = 1000)
        {
            var replenishmentPeriod = (millisecondsToReplenish <= 0)
                ? TimeSpan.FromSeconds(1)
                : TimeSpan.FromMilliseconds(millisecondsToReplenish);

            Factory = () => new WebsocketInvokeSettings(rateTokensPerPeriod, rateTokenLimit, 1, queueProcessingOrder, replenishmentPeriod);
        }

        public static Func<WebsocketInvokeSettingsAttribute> Default { get; } = () => new();
    }
}
