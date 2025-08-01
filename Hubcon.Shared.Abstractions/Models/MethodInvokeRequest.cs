using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    public sealed class OperationRequest : IOperationRequest
    {
        public string ContractName { get; set; }
        public string OperationName { get; set; }
        public Dictionary<string, object?>? Arguments { get; set; }

        public OperationRequest()
        {
            
        }

        public OperationRequest(string operationName, string contractName)
        {
            OperationName = operationName;
            ContractName = contractName;
            Arguments = [];
        }

        public OperationRequest(string methodName, string contractName, Dictionary<string, object?>? args)
        {
            OperationName = methodName;
            ContractName = contractName;
            Arguments = args ?? [];
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is null || obj.GetType() != GetType())
                return false;

            var other = (OperationRequest)obj;

            return string.Equals(ContractName, other.ContractName, StringComparison.Ordinal) &&
                   string.Equals(OperationName, other.OperationName, StringComparison.Ordinal);
        }

        public override int GetHashCode() => HashCode.Combine(ContractName ?? string.Empty, OperationName ?? string.Empty);

    }
}
