using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;

namespace Hubcon.Server.Core.Supervisor
{
    public class ConnectionSupervisor : IConnectionSupervisor, IDisposable
    {
        private readonly ConcurrentDictionary<string, (DateTime Expiration, Action cancellationCallback)> _connections
            = new();

        private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
        private readonly Timer _timer;
        private readonly TimeSpan _checkInterval;

        public ConnectionSupervisor(TimeSpan? checkInterval = null)
        {
            _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
            _timer = new Timer(async _ => await CleanupExpiredAsync(), null, _checkInterval, _checkInterval);
        }

        public bool IsExpired(string id)
        {
            if(_connections.TryGetValue(id, out (DateTime Expiration, Action cancellationCallback) connection))
            {
                return connection.Expiration > DateTime.Now;
            }

            return true;
        }

        public void Register(string id, DateTime expiration, Action cancellationCallback)
        {
            _connections[id] = (expiration, cancellationCallback);
        }

        public void UpdateExpiration(string id, DateTime newExpiration)
        {
            if (_connections.TryGetValue(id, out var entry))
            {
                _connections[id] = (newExpiration, entry.cancellationCallback);
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
                    entry.cancellationCallback();
                }
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        private async Task CleanupExpiredAsync()
        {
            var now = DateTime.UtcNow;
            var expiredIds = _connections
                .Where(kv => kv.Value.Expiration <= now)
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
                    kv.Value.cancellationCallback();
                }
                finally
                {
                }
            }

            _connections.Clear();
            _cleanupSemaphore.Dispose();
        }
    }
}
