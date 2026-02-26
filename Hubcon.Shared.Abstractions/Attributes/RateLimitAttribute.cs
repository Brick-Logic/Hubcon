using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Add rate limiting to a hubcon contract or endpoint. If used in a contract, every endpoint will receive it's own rate limiter. 
    /// Endpoint rate limiters will override the contract's one. Clients will automatically use this to limit itself.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Interface)]
    public class RateLimitAttribute : Attribute, IConfigurationAttribute
    {
        /// <summary>
        /// Gets the token bucket rate limiter instance configured for this attribute.
        /// </summary>
        public TokenBucketRateLimiter RateBucket { get; }

        /// <summary>
        /// Gets the default number of requests allowed within the replenishment period.
        /// </summary>
        public int Requests { get; }

        /// <summary>
        /// Gets the duration, in milliseconds, for the rate limiter to replenish tokens.
        /// </summary>
        public int MillisecondsToReplenish { get; }

        /// <summary>
        /// Gets the maximum number of tokens that can be stored in the token bucket.
        /// </summary>
        public int RateTokenLimit { get; }

        /// <summary>
        /// Gets the maximum number of requests that can be queued if the rate limit is exceeded.
        /// </summary>
        public int QueueLimit { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitAttribute"/> class with specified limits and periods.
        /// </summary>
        /// <param name="requests">The maximum number of requests allowed within the period.</param>
        /// <param name="millisecondsToReplenish">The period, in milliseconds, to replenish tokens.</param>
        /// <param name="rateTokenLimit">The maximum number of tokens in the bucket; defaults to individual request limit if set to 0.</param>
        /// <param name="queueLimit">The maximum number of requests that can be queued; defaults to individual request limit if set to 0.</param>
        public RateLimitAttribute(
            int requests = 5,
            int millisecondsToReplenish = 1000,
            int rateTokenLimit = 0,
            int queueLimit = 10)
        {
            static int GetOrDefault(int limit, int defaultLimit)
            {
                return limit switch
                {
                    0 => defaultLimit,
                    var l => l
                };
            }

            // Initializes the token bucket rate limiter with configured parameters.
            RateBucket = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = GetOrDefault(rateTokenLimit == 0 ? requests : rateTokenLimit, 5),
                TokensPerPeriod = GetOrDefault(requests, 5),
                ReplenishmentPeriod = millisecondsToReplenish == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(millisecondsToReplenish),
                AutoReplenishment = true,
                QueueLimit = GetOrDefault(queueLimit, 10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

            Requests = requests;
            MillisecondsToReplenish = millisecondsToReplenish;
            RateTokenLimit = rateTokenLimit;
            QueueLimit = queueLimit;
        }
    }
}