using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    public record class OperationRequest : IOperationRequest
    {
        public string ContractName { get; set; }
        public string OperationName { get; set; }
        public IEnumerable<object> Args { get; set; }

        public OperationRequest()
        {
            
        }

        public OperationRequest(string operationName, string contractName)
        {
            OperationName = operationName;
            ContractName = contractName;
            Args = Enumerable.Empty<object>();
        }

        public OperationRequest(string methodName, string contractName, IEnumerable<object>? args)
        {
            OperationName = methodName;
            ContractName = contractName;
            Args = args ?? Enumerable.Empty<object>();
        }
    }
}
