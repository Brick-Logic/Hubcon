using Hubcon.Shared.Core.Websockets.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Resilience
{
    internal sealed class RetryableMessage : IRetryableMessage, IAwaitableAckMessage
    {
        private readonly object _message;
        private readonly TimeSpan timeout;
        private readonly int maxRetries;
        private int _completed = 0;
        private bool firstReadPassed = false;
        private readonly TaskCompletionSource<bool> _tcs = new();

        public RetryableMessage(object message, int retries, TimeSpan timeout)
        {
            _message = message;
            RetryCount = retries;
            maxRetries = retries;
            this.timeout = timeout;
        }

        public int RetryCount { get; private set; }

        public Task AckAsync()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
                _tcs.TrySetResult(true);

            return Task.CompletedTask;
        }

        public Task FailedAckAsync()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
                _tcs.TrySetResult(false);

            return Task.CompletedTask;
        }

        public void GetPayload(out object? message)
        {
            if (RetryCount > 0 && Volatile.Read(ref _completed) == 0)
            {
                RetryCount--;
                message = _message;
            }
            else
            {
                message = null;
            }
        }

        public async Task<bool> CanRetry()
        {
            if (firstReadPassed)
                await Task.Delay(timeout / maxRetries);
            else
                firstReadPassed = true;

            if (RetryCount > 0 && Volatile.Read(ref _completed) == 0)
                return true;

            if (Volatile.Read(ref _completed) == 0)
                await FailedAckAsync();

            return false;
        }

        public async Task<bool> WaitAckAsync()
        {
            if (Volatile.Read(ref _completed) == 1)
            {
                var result = _tcs.Task.Result;
                return result;
            }

            var completed = await Task.WhenAny(_tcs.Task, Task.Delay(timeout));

            if (completed != _tcs.Task)
            {
                await FailedAckAsync();
                return false;
            }

            return await _tcs.Task;
        }
    }

}