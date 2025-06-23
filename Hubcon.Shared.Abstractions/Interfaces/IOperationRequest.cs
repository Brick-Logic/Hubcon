using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationRequest
    {
        Dictionary<string, object?>? Arguments { get; }
        string ContractName { get; }
        string OperationName { get; }
    }
}
