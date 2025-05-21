using Hubcon.Core.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Core.Invocation
{
    public class MethodInvokeRequest : IOperationRequest
    {
        public string ContractName { get; }
        public string OperationName { get; }
        public IEnumerable<JsonElement> Args { get; set; }

        public MethodInvokeRequest(string operationName, string contractName)
        {
            OperationName = operationName;
            ContractName = contractName;
            Args = new List<JsonElement>();
        }

        public MethodInvokeRequest(string methodName, string contractName, IEnumerable<JsonElement>? args)
        {
            OperationName = methodName;
            ContractName = contractName;
            Args = args ?? Array.Empty<JsonElement>();
        }
    }
}
