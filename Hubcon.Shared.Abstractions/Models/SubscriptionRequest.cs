using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    public class SubscriptionRequest : IOperationRequest
    {
        public string ContractName { get; }
        public string OperationName { get; }

        public IEnumerable<object> Args { get; }

        public SubscriptionRequest(string operationName, string contractName, IEnumerable<object>? args)
        {
            OperationName = operationName;
            ContractName = contractName;
            Args = args ?? Enumerable.Empty<object>();
        }
    }
}
