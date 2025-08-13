using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationRequest : IOperationEndpoint
    {
        Dictionary<string, object> Arguments { get; }
    }
}
