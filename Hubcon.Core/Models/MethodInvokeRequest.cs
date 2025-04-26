using Hubcon.Core.Converters;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Core.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MethodInvokeRequest
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
