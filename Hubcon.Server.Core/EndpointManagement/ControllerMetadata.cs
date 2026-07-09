using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Hubcon.Server.Core.EndpointManagement;

public static class ControllerMetadata
{
    private static FrozenDictionary<string, Type>? _foundControllers;

    public static void Setup(FrozenDictionary<string, Type> foundControllers)
    {
        _foundControllers ??= foundControllers;
    }

    public static IEnumerable<Type> GetAvailableControllers()
    {
        return _foundControllers?.Values ?? Enumerable.Empty<Type>();
    }
}
