#pragma warning disable CS1591
using System;
using System.Threading;
using System.Threading.Tasks;


namespace Hubcon.Shared.Core.Tools
{
    public static class TimeoutHelper
    {
        public static async ValueTask<T?> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
#if NET8_0_OR_GREATER
                return await task.WaitAsync(timeout, System.Threading.TimeProvider.System, cancellationToken);
#else
                cancellationToken.ThrowIfCancellationRequested();

                using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delayTask = Task.Delay(timeout, delayCts.Token);
                var completedTask = await Task.WhenAny(task, delayTask);

                if (completedTask != task) return default!;
                delayCts.Cancel();
                return await task; 
#endif
            }
            catch (Exception)
            {
                return default!;
            }
        }
    }
}
