using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMethodInvokeRequest
    {
        IEnumerable<JsonElement?> Args { get; }
        string ContractName { get; }
        string MethodName { get; }
    }
}