using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Models;

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    public sealed class InternalConcurrencyCheckMiddleware : IPreRequestMiddleware
    {
        private readonly IInternalServerOptions internalServerOptions;
        private readonly ConcurrentDictionary<string, ConcurrencyTracker> _semaphores;

        public InternalConcurrencyCheckMiddleware(IInternalServerOptions internalServerOptions)
        {
            _semaphores = new ConcurrentDictionary<string, ConcurrencyTracker>();
            this.internalServerOptions = internalServerOptions;
            _ = CheckSemaphores();
        }

        public async Task CheckSemaphores()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

            while (await timer.WaitForNextTickAsync())
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var entry in _semaphores.Where(x => (x.Value.LastUsage + 1200) < now))
                {
                    if (_semaphores.TryGetValue(entry.Key, out var tracker) && tracker.CurrentCount == internalServerOptions.MaxConcurrentOperations)
                    {                
                        _semaphores.TryRemove(entry.Key, out var removed);
                        removed?.Dispose();
                    }
                }
            }
        }

        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            if(internalServerOptions.MaxConcurrentOperations == 0)
            {
                await next();
                return;
            }

            bool allowed = false;
            ConcurrencyTracker tracker = _semaphores.GetOrAdd(context.HttpContext!.Connection.RemoteIpAddress?.ToString()!, 
                x => new ConcurrencyTracker(internalServerOptions.MaxConcurrentOperations));

            try
            {
                allowed = await tracker.WaitAsync();

                if (!allowed)
                {
                    context.Response = HubconResponse.TooManyRequests();
                    return;
                }

                await next();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
               if(allowed)
                    tracker.Release();
            }
        }

        public class ConcurrencyTracker
        {
            readonly SemaphoreSlim _dedicatedSemaphore;

            public long _lastUsage;
            public long LastUsage => _lastUsage;

            public int CurrentCount => _dedicatedSemaphore.CurrentCount;

            public ConcurrencyTracker(int count)
            {
                _dedicatedSemaphore = new SemaphoreSlim(count, count);
            }

            public Task<bool> WaitAsync(CancellationToken cancellationToken = default)
            {
                Interlocked.Exchange(ref _lastUsage, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                return _dedicatedSemaphore.WaitAsync(5000, cancellationToken);
            }

            public void Release()
            {
                Interlocked.Exchange(ref _lastUsage, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                _dedicatedSemaphore.Release();
            }

            public void Dispose()
            {
                _dedicatedSemaphore.Dispose();
            }
        }
    }
}
