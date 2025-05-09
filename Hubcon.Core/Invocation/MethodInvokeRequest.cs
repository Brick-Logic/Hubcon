using Hubcon.Core.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Core.Invocation
{
    public class MethodInvokeRequest : IMethodInvokeRequest
    {
        public string ContractName { get; }
        public string MethodName { get; }
        public IEnumerable<JsonElement?> Args { get; private set; }

        public MethodInvokeRequest(string methodName, string contractName, IEnumerable<JsonElement?>? args = null)
        {
            MethodName = methodName;
            ContractName = contractName;
            Args = args ?? new List<JsonElement?>();
        }
    }
}
