using Hubcon.Client.Core.Authentication;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Configurations
{
    public class OperationOptions(MemberInfo memberInfo) : IOperationConfigurator, IOperationOptions
    {
        public MemberInfo MemberInfo { get; } = memberInfo;

        public MemberType MemberType { get; } = memberInfo switch
        {
            MethodInfo => MemberType.Method,
            PropertyInfo => MemberType.Property,
            _ => throw new ArgumentException("Unsupported member type", nameof(memberInfo))
        };

        public TransportType TransportType { get; private set; } = TransportType.Default;
        public TokenBucketRateLimiterOptions? RateBucketOptions { get; private set; }
        public bool RateLimiterIsShared { get; private set; }
        public int RequestsPerSecond { get; private set; }

        ConcurrentDictionary<HookType, Func<HookContext, Task>> _hooks = new();
        public IReadOnlyDictionary<HookType, Func<HookContext, Task>> Hooks => _hooks;

        private RateLimiter? _rateBucket;
        public RateLimiter? RateBucket => _rateBucket ??= RateBucketOptions != null ? new TokenBucketRateLimiter(RateBucketOptions) : null;
        
        private Func<RequestValidationContext, Task>? _validationHook;
        
        public bool RemoteCancellationIsAllowed { get; private set; }

        public IOperationConfigurator LimitPerSecond(int requestsPerSecond, bool rateLimiterIsShared = true)
        {
            var requestsPerSec = requestsPerSecond == 0 ? 9999999 : requestsPerSecond;
            RequestsPerSecond = requestsPerSec;
            RateLimiterIsShared = rateLimiterIsShared;

            RateBucketOptions ??= new TokenBucketRateLimiterOptions()
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

        public IOperationConfigurator UseTransport(TransportType transportType)
        {
            TransportType = transportType;
            return this;
        }

        public IOperationConfigurator AllowRemoteCancellation(bool value = true)
        {
            RemoteCancellationIsAllowed = value;
            return this;
        }

        public IOperationConfigurator AddHook(HookType hookType, Func<HookContext, Task> hookDelegate)
        {
            _hooks.TryAdd(hookType, hookDelegate);
            return this;
        }

        public Task CallHook(HookContext context)
        {
            if (_hooks.TryGetValue(context.Type, out var hookDelegate))
            {
                return hookDelegate(context);
            }

            return Task.CompletedTask;
        }

        public Task CallHook(HookType type, IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken, object? result = null, Exception? exception = null)
        {
            if (_hooks.TryGetValue(type, out var hookDelegate))
            {
                return hookDelegate(new HookContext(type, services, request, cancellationToken, result, exception));
            }

            return Task.CompletedTask;
        }

        public IOperationConfigurator AddValidationHook(Func<RequestValidationContext, Task> value)
        {
            _validationHook ??= value;
            return this;
        }

        public Task CallValidationHook(IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken)
        {
            if (_validationHook != null)
            {
                return _validationHook(new RequestValidationContext(services, request, cancellationToken));
            }

            return Task.CompletedTask;
        }
    }
}