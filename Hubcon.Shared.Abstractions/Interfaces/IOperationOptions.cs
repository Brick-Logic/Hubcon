using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

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
        IReadOnlyDictionary<HookType, Func<InvocationContext, Task>> Hooks { get; }
        bool? RemoteCancellationIsAllowed { get; }
        bool? HttpAuthIsEnabled { get; }

        Task CallHook(HookType hookType, InvocationContext context);

        Task CallValidationHook(IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken);
    }
}
