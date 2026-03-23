using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Hubcon
{
    /// <summary>
    /// Defines the contract for a high-performance serialization and dynamic type conversion engine.
    /// Provides methods for object-to-JSON mapping, stream transformation, and byte-level serialization.
    /// </summary>
    public interface IDynamicConverter
    {
        /// <summary>
        /// Gets a thread-safe cache of delegate parameter types to optimize dynamic invocation.
        /// </summary>
        ConcurrentDictionary<Delegate, Type[]> TypeCache { get; }

        /// <summary>
        /// Transforms an asynchronous stream of <see cref="JsonElement"/> into a typed asynchronous stream.
        /// </summary>
        /// <typeparam name="T">The target type for the stream elements.</typeparam>
        /// <param name="stream">The source stream of JSON elements.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> containing the deserialized objects.</returns>
        IAsyncEnumerable<T> ConvertStream<T>(IAsyncEnumerable<JsonElement> stream, CancellationToken cancellationToken);

        /// <summary>
        /// Transforms an asynchronous stream of objects into a stream of <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="stream">The source stream of objects.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="JsonElement"/>.</returns>
        IAsyncEnumerable<JsonElement> ConvertToJsonElementStream(IAsyncEnumerable<object?> stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserializes a collection of raw arguments into their corresponding target types.
        /// </summary>
        /// <param name="types">The sequence of target <see cref="Type"/> objects.</param>
        /// <param name="args">The raw argument values.</param>
        /// <returns>A collection of deserialized objects.</returns>
        IEnumerable<object?> DeserializeArgs(IEnumerable<Type> types, IEnumerable<object?> args);

        /// <summary>
        /// Deserializes a byte array into an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="bytes">The UTF-8 encoded byte array.</param>
        /// <returns>The deserialized object.</returns>
        T DeserializeByteArray<T>(byte[] bytes);

        /// <summary>
        /// Deserializes a collection of arguments based on the parameter signature of the specified delegate.
        /// </summary>
        /// <param name="del">The delegate whose parameters define the target types.</param>
        /// <param name="args">The raw argument values.</param>
        /// <returns>A collection of objects ready for delegate invocation.</returns>
        IEnumerable<object?> DeserializedArgs(Delegate del, IEnumerable<object?> args);

        /// <summary>
        /// Deserializes raw data from an object (typically a <see cref="JsonElement"/> or similar) into type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="data">The raw data object.</param>
        /// <returns>The deserialized object.</returns>
        T DeserializeData<T>(object? data);

        /// <summary>
        /// Deserializes a collection of <see cref="JsonElement"/> into a collection of typed objects.
        /// </summary>
        /// <param name="elements">The JSON elements to deserialize.</param>
        /// <param name="types">The target types for each element.</param>
        /// <returns>A collection of deserialized objects.</returns>
        IEnumerable<object?> DeserializeJsonArgs(IEnumerable<JsonElement> elements, IEnumerable<Type> types);

        /// <summary>
        /// Deserializes a single <see cref="JsonElement"/> into the specified <paramref name="targetType"/>.
        /// </summary>
        /// <param name="element">The JSON element.</param>
        /// <param name="targetType">The type to deserialize into.</param>
        /// <returns>The deserialized object.</returns>
        object? DeserializeJsonElement(JsonElement element, Type targetType);

        /// <summary>
        /// Deserializes a single <see cref="JsonElement"/> into type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="element">The JSON element.</param>
        /// <returns>The deserialized object.</returns>
        T? DeserializeJsonElement<T>(JsonElement element);

        /// <summary>
        /// Serializes a collection of objects into their JSON element representations.
        /// </summary>
        /// <param name="values">The objects to serialize.</param>
        /// <returns>A collection of <see cref="JsonElement"/>.</returns>
        IEnumerable<JsonElement> SerializeArgsToJson(IEnumerable<object?> values);

        /// <summary>
        /// Serializes an object into a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <returns>A <see cref="JsonElement"/> representation of the object.</returns>
        JsonElement SerializeObject(object? value);

        /// <summary>
        /// Serializes an object of type <typeparamref name="T"/> into a JSON string.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>A JSON string.</returns>
        string Serialize<T>(T value);

        /// <summary>
        /// Serializes a value of type <typeparamref name="T"/> into a <see cref="JsonElement"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>A <see cref="JsonElement"/>.</returns>
        JsonElement SerializeToElement<T>(T value);

        /// <summary>
        /// Deserializes an instance of <typeparamref name="T"/> from a JSON string.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>The deserialized object.</returns>
        T DeserializeFromString<T>(string? json);

        /// <summary>
        /// Serializes a value into a <see cref="ReadOnlySpan{T}"/> of bytes using a provided <see cref="System.Buffers.ArrayBufferWriter{T}"/>.
        /// Useful for high-performance, low-allocation scenarios.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="bufferWriter">The buffer writer to use for serialization.</param>
        /// <returns>A read-only span containing the serialized data.</returns>
        ReadOnlySpan<byte> SerializeToSpan<T>(T value, ArrayBufferWriter<byte> bufferWriter);

        /// <summary>
        /// Serializes a value directly into a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="writer">The destination JSON writer.</param>
        /// <param name="message">The message to serialize.</param>
        void Serialize<T>(Utf8JsonWriter writer, T? message);

        /// <summary>
        /// Parses a raw JSON string into a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="rawData">The raw JSON string.</param>
        /// <returns>A <see cref="JsonElement"/>.</returns>
        JsonElement ToJsonElement(string rawData);
    }
}
