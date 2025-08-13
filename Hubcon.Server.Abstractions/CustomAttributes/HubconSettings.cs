using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public abstract class HubconSettings
    {
        public abstract TokenBucketRateLimiter RateBucket { get; }
    }
}
