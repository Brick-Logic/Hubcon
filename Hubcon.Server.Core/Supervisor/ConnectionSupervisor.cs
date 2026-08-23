using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Heartbeat;

#pragma warning disable CS1591

namespace Hubcon.Server.Core.Supervisor
{
    public sealed class ConnectionSupervisor : IConnectionSupervisor, IDisposable
    {
        private readonly AtomicPass _disposed = new AtomicPass(); 
        private readonly ConcurrentDictionary<string, ConnectionSupervisionMetadata> _connections = new();

        private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
        private readonly TimeSpan _checkInterval;
        private readonly Task supervisorTask;  

        public ConnectionSupervisor(TimeSpan? checkInterval = null)
        {
            _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
            supervisorTask = CleanupExpiredAsync();
        }

        public void Register(string id, long expiration, long heartbeatExpiration, Action cancellationCallback)
        {
            _connections[id] = new ConnectionSupervisionMetadata(id, expiration, heartbeatExpiration, cancellationCallback);
        }

        public void UpdateExpiration(string id, long newExpiration)
        {
            if (_connections.TryGetValue(id, out var entry))
            {
                entry.Expiration = newExpiration;
            }
        }
        
        public void NotifyAlive(string id, long newExpiration)
        {
            if (_connections.TryGetValue(id, out var entry))
            {
                entry.HeartbeatExpiration = newExpiration;
            }
        }
        
        public async Task UnregisterAsync(string id)
        {
            await _cleanupSemaphore.WaitAsync();
            try
            {
                if (_connections.TryRemove(id, out var entry))
                {
                    try
                    {
                        entry.CancellationCallback();
                    }
                    catch
                    {
                        // Ignored
                    }
                }
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        private async Task CleanupExpiredAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            
            while (!_disposed.WasAcquired)
            {
                try
                {
                    var nowTimeInSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    var expiredIds = _connections
                        .Where(kv => kv.Value.IsExpired(nowTimeInSeconds))
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var id in expiredIds)
                    {
                        await UnregisterAsync(id);
                    }
                }
                catch
                {
                    // Ignored
                }

                await timer.WaitForNextTickAsync();
            }
        }

        public void Dispose()
        {
            if (!_disposed.TryAcquirePass()) return;
                
            foreach (var kv in _connections)
            {
                try
                {
                    kv.Value.CancellationCallback();
                }
                catch
                {
                    // Ignored
                }
            }

            _connections.Clear();
            _cleanupSemaphore.Dispose();
        }
    }
    
    public sealed class ConnectionSupervisionMetadata
    {
        public ConnectionSupervisionMetadata(string id, long expiration, long heartbeatExpiration, Action cancellationCallback)
        {
            Id = id;
            Expiration = expiration;
            HeartbeatExpiration = heartbeatExpiration;
            CancellationCallback = cancellationCallback;
        }
        
        public string Id { get; }
        public long Expiration { private get; set; }
        public long HeartbeatExpiration { private get; set; }
        public Action CancellationCallback { get; }
        
        public bool IsExpired() 
        {
            var nowTimeInSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Expiration < nowTimeInSeconds || HeartbeatExpiration < nowTimeInSeconds;
        }
        
        public bool IsExpired(long nowTimeInSeconds) 
        {
            return Expiration < nowTimeInSeconds || HeartbeatExpiration < nowTimeInSeconds;
        }
    }
}
