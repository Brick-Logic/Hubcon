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

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is null || obj.GetType() != GetType())
                return false;

            var other = (SubscriptionRequest)obj;

            return string.Equals(ContractName, other.ContractName, StringComparison.Ordinal) &&
                   string.Equals(OperationName, other.OperationName, StringComparison.Ordinal);
        }

        public override int GetHashCode() => HashCode.Combine(ContractName ?? string.Empty, OperationName ?? string.Empty);
    }
}
