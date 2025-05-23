using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationRequest
    {
        IEnumerable<JsonElement> Args { get; }
        string ContractName { get; }
        string OperationName { get; }
    }
}
