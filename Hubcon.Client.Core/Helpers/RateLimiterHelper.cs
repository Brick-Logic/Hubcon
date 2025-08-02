using Hubcon.Client.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Helpers
{
    public static class RateLimiterHelper
    {
        public static Task AcquireAsync(IClientOptions? options, RateLimiter? globalLimiter, RateLimiter? typeLimiter = null, RateLimiter? operationLimiter = null)
        {
            if (options == null || options.LimitersDisabled)
                return Task.CompletedTask;

            if (globalLimiter == null && typeLimiter == null && operationLimiter == null)
                return Task.CompletedTask;

            return AcquireInternalAsync(globalLimiter, typeLimiter, operationLimiter);
        }

        private static async Task AcquireInternalAsync(RateLimiter? globalLimiter, RateLimiter? typeLimiter, RateLimiter? operationLimiter)
        {
            if (globalLimiter != null) await globalLimiter.AcquireAsync().ConfigureAwait(false);
            if (typeLimiter != null) await typeLimiter.AcquireAsync().ConfigureAwait(false);
            if (operationLimiter != null) await operationLimiter.AcquireAsync().ConfigureAwait(false);
        }
    }
}
