using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;
using Hubcon.Shared.Core.Tools;
#pragma warning disable CS1591

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    public sealed class InternalConcurrencyCheckMiddleware : IPreRequestMiddleware, IDisposable
    {
        private readonly IInternalServerOptions _internalServerOptions;
        private readonly ConcurrentDictionary<string, IPConcurrencyTracker> _trackers;
        private readonly PeriodicTimer _cleanupTimer;
        private readonly Task _cleanupTask;

        public InternalConcurrencyCheckMiddleware(IInternalServerOptions internalServerOptions)
        {
            _internalServerOptions = internalServerOptions;
            _trackers = new ConcurrentDictionary<string, IPConcurrencyTracker>();
            _cleanupTimer = new PeriodicTimer(TimeSpan.FromMinutes(10));
            _cleanupTask = RunCleanupLoopAsync();
        }

        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            var settings = _internalServerOptions.GetTransportSettings(context.TransportType);

            var maxConcurrentPerIp = settings.MaxConcurrentRequestsPerIp;

            if (maxConcurrentPerIp <= 0)
            {
                await next();
                return;
            }

            var ipAddress = context.HttpContext?.Connection.RemoteIpAddress?.ToString()
                            ?? context.HttpContext?.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown";

            var tracker = _trackers.GetOrAdd(
                ipAddress,
                static (_, maxLimit) => new IPConcurrencyTracker(maxLimit),
                maxConcurrentPerIp);

            if (!tracker.Counter.TryIncrement())
            {
                context.Response = HubconResponse.TooManyRequests();
                return;
            }

            tracker.UpdateLastUsage();

            try
            {
                await next();
            }
            finally
            {
                tracker.Counter.Decrement();
                tracker.UpdateLastUsage();
            }
        }

        private async Task RunCleanupLoopAsync()
        {
            while (await _cleanupTimer.WaitForNextTickAsync())
            {
                long now = Environment.TickCount64;

                long expirationThreshold = 1_200_000;

                foreach (var pair in _trackers)
                {
                    if (now - pair.Value.LastUsageTicks > expirationThreshold && pair.Value.Counter.Value == 0)
                    {
                        _trackers.TryRemove(pair.Key, out _);
                    }
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer.Dispose();
        }

        private sealed class IPConcurrencyTracker
        {
            public AtomicCounter Counter { get; }
            private long _lastUsageTicks;

            public long LastUsageTicks => Volatile.Read(ref _lastUsageTicks);

            public IPConcurrencyTracker(int maxConcurrentRequests)
            {
                Counter = new AtomicCounter(maxConcurrentRequests);
                UpdateLastUsage();
            }

            public void UpdateLastUsage()
            {
                Volatile.Write(ref _lastUsageTicks, Environment.TickCount64);
            }
        }
    }
}