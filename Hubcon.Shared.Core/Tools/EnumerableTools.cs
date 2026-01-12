using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Tools
{
    public static class EnumerableTools
    {
        public static bool IsAsyncEnumerable(object? obj)
        {
            if (obj is null) return false;

            return obj.GetType()
                      .GetInterfaces()
                      .Any(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }

        public static Type? GetAsyncEnumerableType(object obj)
        {
            if (obj is null) return null;

            var type = obj.GetType();

            var asyncEnumInterface = type
                .GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

            return asyncEnumInterface;
        }

        public static Type? GetAsyncEnumeratorType(object obj)
        {
            if (obj is null) return null;

            return obj.GetType()
                      .GetInterfaces()
                      .FirstOrDefault(i =>
                          i.IsGenericType &&
                          i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>))
                      ?.GetGenericArguments()[0];
        }

        //public static IAsyncEnumerable<JsonElement>? WrapEnumeratorAsJsonElementEnumerable(object enumeratorObj, CancellationToken ct)
        //{
        //    if (enumeratorObj is null) return null;

        //    var t = GetAsyncEnumeratorType(enumeratorObj);
        //    if (t == null) return null;

        //    // Obtenemos el método genérico que hará el cast y aplicará el token
        //    var method = typeof(EnumerableTools)
        //        .GetMethod(nameof(WrapToJsonElementWithCancellation), BindingFlags.Static | BindingFlags.Public)!
        //        .MakeGenericMethod(t);

        //    // Invocamos pasando el objeto fuente Y el token
        //    return (IAsyncEnumerable<JsonElement>)method.Invoke(null, new[] { enumeratorObj, ct })!;
        //}

        private static Func<object, Type, JsonTypeInfo, CancellationToken, IAsyncEnumerable<JsonElement>>? AsyncEnumerableWrapper;
        public static void SetupEnumerableWrapper(Func<object, Type, JsonTypeInfo, CancellationToken, IAsyncEnumerable<JsonElement>> wrapper) 
            => AsyncEnumerableWrapper ??= wrapper;
        public static IAsyncEnumerable<JsonElement>? Wrap(object source, CancellationToken ct)
        {
            Type t = GetAsyncEnumerableType(source)!;
            Type i = GetAsyncEnumeratorType(source)!;
            JsonTypeInfo info = DynamicConverter.JsonSerializerOptions.TypeInfoResolver!.GetTypeInfo(i, DynamicConverter.JsonSerializerOptions)!;

            return AsyncEnumerableWrapper!.Invoke(source, i, info, ct);
        }
        public static async IAsyncEnumerable<JsonElement> GenericYieldWrapper<T>(IAsyncEnumerable<T> source, JsonTypeInfo typeInfo, [EnumeratorCancellation] CancellationToken ct)
        {
            // Casting seguro del JsonTypeInfo para evitar reflexión en el Serialize
            var info = (JsonTypeInfo<T>)typeInfo;

            await foreach (var item in source.WithCancellation(ct))
            {
                // AOT ama esto porque T está definido en el punto de llamada
                yield return JsonSerializer.SerializeToElement(item, info);
            }
        }

        public static async IAsyncEnumerable<JsonElement> WrapToJsonElementWithCancellation<T>(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken ct)
        {
            var typeInfo = DynamicConverter.JsonSerializerOptions.TypeInfoResolver!.GetTypeInfo(typeof(T), DynamicConverter.JsonSerializerOptions);
            // Usamos WithCancellation para que el iterador interno (el ingest) se entere del cierre
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
            {
                // Aquí tu lógica de conversión a JsonElement (serialización manual o JsonSerializer)
                yield return JsonSerializer.SerializeToElement(item, typeInfo!);
            }
        }

        public static object? GetAsyncEnumeratorViaReflection(object source)
        {
            if (source == null) return null;

            var asyncEnumerableInterface = source.GetType()
                .GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

            if (asyncEnumerableInterface == null)
                return null;

            var method = asyncEnumerableInterface
                .GetMethod("GetAsyncEnumerator", new[] { typeof(CancellationToken) });

            if (method == null)
                return null;

            return method.Invoke(source, new object[] { CancellationToken.None });
        }

        public static object ConvertAsyncEnumerableDynamic(
            Type targetType,
            IAsyncEnumerable<JsonElement> source,
            IDynamicConverter converter)
        {
            var thisType = typeof(EnumerableTools);

            var method = thisType
                .GetMethod(nameof(ConvertAsyncEnumerable), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(targetType.GetGenericArguments()[0]);

            var enumerable = method.Invoke(null, new object[] { source, converter });
            return enumerable;
        }


        public static async IAsyncEnumerable<T> ConvertAsyncEnumerable<T>(
            IAsyncEnumerable<JsonElement> source,
            IDynamicConverter converter)
        {
            await foreach (var item in source)
            {
                yield return converter.DeserializeJsonElement<T>(item)!;
            }
        }
    }
}
