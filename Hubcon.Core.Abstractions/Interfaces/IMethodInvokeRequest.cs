using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IOperationRequest
    {
        IEnumerable<JsonElement?> Args { get; }
        string ContractName { get; }
        string OperationName { get; }
    }
}