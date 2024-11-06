using MessagePack;
using System.ComponentModel;

namespace Hubcon.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MessagePackObject]
    public record MethodInvokeInfo([property: Key(0)] string MethodName, [property: Key(1)] object?[] Args);
}
