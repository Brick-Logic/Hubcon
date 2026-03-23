#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IAsyncObserver<T> : IObserver<T>
    {
        Task<bool> WriteToChannelAsync(T item);
        IAsyncEnumerable<T> GetAsyncEnumerable(Action? disposeAction = null);
        Task WaitUntilCompleted();
        Task<T> ReadItemAsync(CancellationToken cancellationToken = default);
    }
}
