using Hubcon.Converters;
using MessagePack;
using System.ComponentModel;

namespace Hubcon.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MessagePackObject]
    public class MethodInvokeInfo
    {
        [Key(0)]
        public string MethodName { get; }

        [Key(1)]
        public object?[] Args { get; private set; }

        public MethodInvokeInfo(string methodName, object?[]? args = null)
        {
            MethodName = methodName;
            Args = args ?? [];
        }

        public MethodInvokeInfo SerializeArgs()
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
