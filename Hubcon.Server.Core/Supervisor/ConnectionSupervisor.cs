using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;
using Hubcon.Shared.Core.Websockets.Heartbeat;

#pragma warning disable CS1591

namespace Hubcon.Server.Core.Supervisor
{
    public class ConnectionSupervisor : IConnectionSupervisor, IDisposable
    {
        private readonly ConcurrentDictionary<string, ConnectionSupervisionMetadata> _connections = new();

        private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
        private readonly Timer _timer;
        private readonly TimeSpan _checkInterval;

        public ConnectionSupervisor(TimeSpan? checkInterval = null)
        {
            _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
            _timer = new Timer(async _ => await CleanupExpiredAsync(), null, _checkInterval, _checkInterval);
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
            await RemoveConnectionAsync(id);
        }

        private async Task RemoveConnectionAsync(string id)
        {
            await _cleanupSemaphore.WaitAsync();
            try
            {
                if (_connections.TryRemove(id, out var entry))
                {
                    entry.CancellationCallback();
                }
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        private async Task CleanupExpiredAsync()
        {
            var nowTimeInSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var expiredIds = _connections
                .Where(kv => kv.Value.IsExpired(nowTimeInSeconds))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var id in expiredIds)
            {
                await RemoveConnectionAsync(id);
            }
        }

        public void Dispose()
        {
            _timer.Dispose();

            foreach (var kv in _connections)
            {
                try
                {
                    kv.Value.CancellationCallback();
                }
                finally
                {
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
