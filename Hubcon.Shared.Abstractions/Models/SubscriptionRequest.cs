using Hubcon.Shared.Abstractions.Interfaces;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SubscriptionRequest : IOperationRequest
    {
        public string ContractName { get; }
        public string OperationName { get; }
        public Dictionary<string, object> Arguments { get; }

        public SubscriptionRequest(string operationName, string contractName, Dictionary<string, object>? arguments)
        {
            OperationName = operationName;
            ContractName = contractName;
            Arguments = arguments ?? [];
        }
    }
}
