using Hubcon.Core.Converters;
using System.ComponentModel;

namespace Hubcon.Core.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MethodInvokeRequest
    {
        public string? HandlerMethodName { get; }
        public string MethodName { get; }
        public object?[] Args { get; private set; }

        public MethodInvokeRequest(string methodName, string? handlerMethodName, object?[]? args = null)
        {
            MethodName = methodName;
            HandlerMethodName = handlerMethodName;
            Args = args ?? new List<object>().ToArray();
        }

        public MethodInvokeRequest SerializeArgs()
        {
            Args = DynamicConverter.SerializeArgs(Args);
            return this;
        }

        public object?[] GetDeserializedArgs(Delegate del)
        {
            return DynamicConverter.DeserializedArgs(del, Args);
        }
    }
}
