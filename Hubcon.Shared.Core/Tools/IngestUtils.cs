using Hubcon.Shared.Abstractions.Interfaces;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Shared.Core.Tools
{
    public static class IngestUtils
    {
        public static IAsyncEnumerable<JsonElement> WrapAsJsonElementEnumerable(object value, Type elementType, IDynamicConverter converter)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (!typeof(IAsyncEnumerable<>).MakeGenericType(elementType).IsAssignableFrom(value.GetType()))
                throw new InvalidCastException($"Expected IAsyncEnumerable<{elementType.Name}> but got {value.GetType().Name}");

            var method = typeof(IngestUtils)
                .GetMethod(nameof(WrapGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(elementType);

            return (IAsyncEnumerable<JsonElement>)method.Invoke(null, new object[] { value, converter })!;
        }

        private static async IAsyncEnumerable<JsonElement> WrapGeneric<T>(IAsyncEnumerable<T> source, IDynamicConverter converter)
        {
            await foreach (var item in source)
            {
                var json = converter.SerializeObject(item);
                yield return json;
            }
        }
    }
}
