using MessagePack;
using System.ComponentModel;

namespace Hubcon.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MessagePackObject]
    public record class MethodResponse([property: Key(0)] bool Success, [property: Key(1)] object? Data = null);
}
