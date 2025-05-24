using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Models
{
    [JsonSerializable(typeof(SubscriptionRequest))]
    public class SubscriptionRequest : IOperationRequest
    {
        public string ContractName { get; }
        public string OperationName { get; }

        public IEnumerable<JsonElement> Args { get; }

        public SubscriptionRequest(string operationName, string contractName, IEnumerable<JsonElement>? args)
        {
            OperationName = operationName;
            ContractName = contractName;
            Args = args ?? Array.Empty<JsonElement>();
        }
    }
}
