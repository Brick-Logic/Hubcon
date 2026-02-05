using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Interface)]
    public class RateLimitAttribute : Attribute
    {
        public TokenBucketRateLimiter RateBucket { get; }
        public int Requests { get; }
        public int MillisecondsToReplenish { get; }
        public int RateTokenLimit { get; }
        public int QueueLimit { get; }

        public RateLimitAttribute(
            int requests = 1000,
            int millisecondsToReplenish = 1000,
            int rateTokenLimit = 0,
            int queueLimit = 0)
        {
            static int GetOrDefault(int limit, int defaultLimit)
            {
                return limit switch
                {
                    0 => defaultLimit,
                    var l => l
                };
            }

            RateBucket = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = GetOrDefault(rateTokenLimit, requests),
                TokensPerPeriod = requests,
                ReplenishmentPeriod = millisecondsToReplenish == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(millisecondsToReplenish),
                AutoReplenishment = true,
                QueueLimit = GetOrDefault(queueLimit, requests),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

            Requests = requests;
            MillisecondsToReplenish = millisecondsToReplenish;
            RateTokenLimit = rateTokenLimit;
            QueueLimit = queueLimit;
        }
    }
}