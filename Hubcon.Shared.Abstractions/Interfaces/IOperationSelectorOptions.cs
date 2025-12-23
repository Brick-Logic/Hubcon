using System.Collections.Concurrent;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IGlobalOperationOptions
    {
        ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; }
    }
}
