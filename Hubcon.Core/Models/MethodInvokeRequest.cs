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

        public MethodInvokeRequest SerializeArgs(Func<object?[], object?[]> serializer)
        {
            Args = serializer.Invoke(Args!);
            return this;
        }

        public object[] GetSerializedArgs(Func<object[], object[]> serializer)
        {
            return serializer.Invoke(Args!);
        }

        public object?[] GetDeserializedArgs(Type[] types, Func<Type[], object?[], object?[]> deserializer)
        {
            return deserializer.Invoke(types, Args!);
        }
    }
}
