#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Hubcon.Shared.Abstractions.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public sealed class OperationRequest : IOperationRequest
    {
        public string ContractName { get; set; }
        public string OperationName { get; set; }
        public IReadOnlyDictionary<string, object> Arguments { get; set; }

        public OperationRequest()
        {

        }

        public OperationRequest(string operationName, string contractName)
        {
            OperationName = operationName;
            ContractName = contractName;
            Arguments = new Dictionary<string, object>();
        }

        public OperationRequest(string methodName, string contractName, IReadOnlyDictionary<string, object>? args)
        {
            OperationName = methodName;
            ContractName = contractName;
            Arguments = args ?? new Dictionary<string, object>();
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

        public void SetOperationName(string operationName)
        {
            OperationName = operationName;
        }

        public void AssignArguments(IReadOnlyDictionary<string, object> arguments)
        {
            this.Arguments = arguments;
        }
    }
}
