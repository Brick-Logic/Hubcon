using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;


namespace Hubcon.Client.Core.Configurations
{
    public class OperationOptions : IOperationConfigurator, IOperationOptions
    {
        public MemberInfo MemberInfo { get; }

        public MemberType MemberType { get; }

        public HubconTransportAttribute? TransportType { get; private set; }
        public TokenBucketRateLimiterOptions? RateBucketOptions { get; private set; }
        public bool RateLimiterIsShared { get; private set; }
        public int RequestsPerSecond { get; private set; }
        public Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; } = new();

        ConcurrentDictionary<HookType, Func<IInvocationContext, Task>> _hooks = new();
        public IReadOnlyDictionary<HookType, Func<IInvocationContext, Task>> Hooks => _hooks;

        private RateLimiter? _rateBucket;
        public RateLimiter? RateBucket => _rateBucket ??= RateBucketOptions != null ? new TokenBucketRateLimiter(RateBucketOptions) : null;

        private Func<RequestValidationContext, Task>? _validationHook;

        public OperationOptions(MemberInfo memberInfo)
        {
            MemberInfo = memberInfo;
            MemberType = memberInfo switch
        {
            MethodInfo => MemberType.Method,
            PropertyInfo => MemberType.Property,
            _ => throw new ArgumentException("Unsupported member type", nameof(memberInfo))
        };
        }

        public bool? RemoteCancellationIsAllowed { get; private set; }

        public bool? AuthIsEnabled { get; private set; }

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

        public IOperationConfigurator UseTransport<T>() where T : HubconTransportAttribute, new()
        {
            TransportType = HubconTransportAttribute.GetDefault<T>();
            return this;
        }

        public IOperationConfigurator UseWebSockets()
        {
            TransportType = HubconTransportAttribute.GetDefault<WebSocketTransport>();
            return this;
        }

        public IOperationConfigurator UseHttp()
        {
            TransportType = HubconTransportAttribute.GetDefault<WebSocketTransport>();
            return this;
        }

        public IOperationConfigurator UseNonHubconHttp()
        {
            TransportType = HubconTransportAttribute.GetDefault<NonHubconHttpTransport>();
            return this;
        }

        public IOperationConfigurator AllowRemoteCancellation(bool value = true)
        {
            RemoteCancellationIsAllowed = value;
            return this;
        }

        public IOperationConfigurator AddHook(HookType hookType, Func<IInvocationContext, Task> hookDelegate)
        {
            _hooks.TryAdd(hookType, hookDelegate);
            return this;
        }

        public Task CallHook(HookType hookType, IInvocationContext context)
        {
            return _hooks.GetOrAdd(hookType, _ => Task.CompletedTask).Invoke(context);
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

        public IOperationConfigurator DisableHttpAuthentication()
        {
            AuthIsEnabled = false;
            return this;
        }

        public IOperationConfigurator ConfigureRateBucket(RateLimitAttribute rateLimitAttribute)
        {
            _rateBucket = rateLimitAttribute.RateBucket;
            return this;
        }

        public IOperationConfigurator AddHeaderProvider(string key, Func<IServiceProvider, string> valueProvider)
        {
            HeaderProviders.TryAdd(key, valueProvider);
            return this;
        }
    }
}