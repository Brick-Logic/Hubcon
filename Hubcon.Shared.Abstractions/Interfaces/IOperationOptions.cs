using Hubcon.Shared.Abstractions.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.RateLimiting;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationOptions
    {
        TransportType TransportType { get; }
        MemberInfo MemberInfo { get; }
        MemberType MemberType { get; }
        TokenBucketRateLimiterOptions? RateBucketOptions { get; }
        int RequestsPerSecond { get; }
        bool RateLimiterIsShared { get; }
        RateLimiter? RateBucket { get; }
    }
}
