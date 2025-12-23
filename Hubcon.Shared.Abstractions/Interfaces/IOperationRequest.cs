using System.Collections.Generic;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationRequest : IOperationEndpoint
    {
        IReadOnlyDictionary<string, object> Arguments { get; }
    }
}
