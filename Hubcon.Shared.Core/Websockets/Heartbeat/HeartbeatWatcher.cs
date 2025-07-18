using System.ComponentModel;

namespace Hubcon.Shared.Core.Websockets.Heartbeat
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HeartbeatWatcher : IAsyncDisposable
    {
        private bool timeoutExecuted = false;
        private readonly Func<Task> _onTimeout;
        private readonly TimeSpan _timeoutSeconds;

        private CancellationTokenSource _cts = new();
        private Task _loop;

        private DateTime _lastHeartbeat = DateTime.UtcNow;

        public HeartbeatWatcher(TimeSpan timeoutSeconds, Func<Task> onTimeout)
        {
            _timeoutSeconds = timeoutSeconds;
            _onTimeout = onTimeout;

            _loop = RunAsync(_cts.Token);
        }

        public void NotifyHeartbeat()
        {
            _lastHeartbeat = DateTime.UtcNow;
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_timeoutSeconds * 1000 / 5, token);

                var elapsed = (DateTime.UtcNow - _lastHeartbeat).TotalSeconds;
                if (TimeSpan.FromSeconds(elapsed) > _timeoutSeconds)
                {
                    if (!timeoutExecuted)
                    {
                        timeoutExecuted = true;
                        await _onTimeout();
                    }
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!timeoutExecuted)
            {
                _cts.Cancel();
                _cts.Dispose();
                timeoutExecuted = true;
                await _onTimeout();
            }

            try { await _loop; } catch { /* swallow */ }
        }
    }

}
