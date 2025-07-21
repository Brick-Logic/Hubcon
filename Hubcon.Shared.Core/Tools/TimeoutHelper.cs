using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Tools
{
    public static class TimeoutHelper
    {
        public static async ValueTask<T> WaitWithTimeoutAsync<T>(TimeSpan timeout, Task<T> task)
        {
            if (timeout == TimeSpan.Zero)
            {
                // No timeout, solo esperar la tarea normalmente
                return await task;
            }

            using var cts = new CancellationTokenSource();

            var delayTask = Task.Delay(timeout, cts.Token);

            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == task)
            {
                cts.Cancel();
                return await task;
            }
            else
            {
                return default!;
            }
        }
    }
}
