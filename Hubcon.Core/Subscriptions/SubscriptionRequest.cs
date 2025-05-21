using Hubcon.Core.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Core.Subscriptions
{
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
