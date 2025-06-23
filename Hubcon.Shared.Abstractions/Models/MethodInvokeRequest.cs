using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    public record class OperationRequest : IOperationRequest
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
    }
}
