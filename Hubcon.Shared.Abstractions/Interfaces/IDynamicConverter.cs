using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IDynamicConverter
    {
        Dictionary<Delegate, Type[]> TypeCache { get; }

        IAsyncEnumerable<T> ConvertStream<T>(IAsyncEnumerable<JsonElement> stream, CancellationToken cancellationToken);
        IAsyncEnumerable<JsonElement> ConvertToJsonElementStream(IAsyncEnumerable<object?> stream, CancellationToken cancellationToken = default);
        IEnumerable<object?> DeserializeArgs(IEnumerable<Type> types, IEnumerable<object?> args);
        T DeserializeByteArray<T>(byte[] bytes);
        IEnumerable<object?> DeserializedArgs(Delegate del, IEnumerable<object?> args);
        object? DeserializeData(Type type, object data);
        T? DeserializeData<T>(object? data);
        IEnumerable<object?> DeserializeJsonArgs(IEnumerable<JsonElement> elements, IEnumerable<Type> types);
        object? DeserializeJsonElement(JsonElement element, Type targetType);
        T? DeserializeJsonElement<T>(JsonElement element);
        object?[] SerializeArgs(object?[] args);
        IEnumerable<JsonElement> SerializeArgsToJson(IEnumerable<object?> values);
        string? SerializeData(object? data);
        JsonElement SerializeObject(object? value);
    }
}
