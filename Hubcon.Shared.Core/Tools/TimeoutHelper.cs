using System;
using System.Threading;
using System.Threading.Tasks;


namespace Hubcon.Shared.Core.Tools
{
    public static class TimeoutHelper
    {

        //public static async ValueTask<T> WaitWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> taskFactory, TimeSpan timeout)
        //{
        //    using var cts = new CancellationTokenSource(timeout);
        //    try
        //    {
        //        return await taskFactory(cts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        return default!;
        //    }
        //}

        //public static async ValueTask WaitWithTimeoutAsync(Func<CancellationToken, Task> taskFactory, TimeSpan timeout)
        //{
        //    using var cts = new CancellationTokenSource(timeout);
        //    try
        //    {
        //        await taskFactory(cts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //    }
        //}

        public static async ValueTask<T?> WaitWithTimeoutAsync<T>(Func<TimeSpan, TimeProvider, CancellationToken, Task<T>> taskFactory, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                return await taskFactory(timeout, TimeProvider.System, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return default!;
            }
            catch (Exception)
            {
                return default!;
            }
        }

        public static async ValueTask WaitWithTimeoutAsync(Func<TimeSpan, TimeProvider, CancellationToken, Task> taskFactory, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            try
            {
                await taskFactory(timeout, TimeProvider.System, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }
    }
}
