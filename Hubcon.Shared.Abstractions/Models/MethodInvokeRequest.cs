using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Models
{
    [JsonSerializable(typeof(OperationRequest))]
    public class OperationRequest : IOperationRequest
    {
        public string ContractName { get; }
        public string OperationName { get; }
        public IEnumerable<JsonElement> Args { get; set; }

        public OperationRequest(string operationName, string contractName)
        {
            OperationName = operationName;
            ContractName = contractName;
            Args = new List<JsonElement>();
        }

        public OperationRequest(string methodName, string contractName, IEnumerable<JsonElement>? args)
        {
            OperationName = methodName;
            ContractName = contractName;
            Args = args ?? Array.Empty<JsonElement>();
        }
    }
}
