using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Tools
{
    public static class TimeoutHelper
    {
        
        public static async ValueTask<T?> WaitWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> taskFactory, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                return await taskFactory(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return default;
            }
        }
    }
}
