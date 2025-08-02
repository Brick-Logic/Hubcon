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
        public static async ValueTask AcquireAsync(IClientOptions? options, RateLimiter? globalLimiter, RateLimiter? typeLimiter = null, RateLimiter? operationLimiter = null)
        {
            if (options == null || options.LimitersDisabled)
                return;

            if (globalLimiter != null) await globalLimiter.AcquireAsync();

            if (typeLimiter != null) await typeLimiter.AcquireAsync();

            if (operationLimiter != null) await operationLimiter.AcquireAsync();
        }
    }
}
