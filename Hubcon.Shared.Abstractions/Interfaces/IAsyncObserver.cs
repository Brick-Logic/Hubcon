using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IAsyncObserver<T> : IObserver<T>
    {
        Task<bool> WriteToChannelAsync(T? item);
        IAsyncEnumerable<T?> GetAsyncEnumerable(CancellationToken cancellationToken);
        Task WaitUntilCompleted();
        Task<T?> ReadItemAsync(CancellationToken cancellationToken = default);
    }
}
