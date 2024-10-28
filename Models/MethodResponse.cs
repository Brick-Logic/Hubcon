using MessagePack;

namespace Hubcon.Models
{
    [MessagePackObject]
    public record class MethodResponse([property: Key(0)] bool Success, [property: Key(1)] object? Data = null);
}
