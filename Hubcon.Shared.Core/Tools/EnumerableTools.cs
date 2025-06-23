using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Tools
{
    public class EnumerableTools
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
        public static IAsyncEnumerable<JsonElement>? WrapEnumeratorAsJsonElementEnumerable(object enumeratorObj)
        {
            if (enumeratorObj is null) return null;

            var t = GetAsyncEnumeratorType(enumeratorObj);
            if (t == null) return null;

            var method = typeof(EnumerableTools)
                .GetMethod(nameof(WrapToJsonElement), BindingFlags.Static | BindingFlags.Public)!
                .MakeGenericMethod(t);

            var enumerator = GetAsyncEnumeratorViaReflection(enumeratorObj);

            return (IAsyncEnumerable<JsonElement>)method.Invoke(null, new[] { enumerator })!;
        }

        public static IAsyncEnumerable<JsonElement> WrapToJsonElement<T>(IAsyncEnumerator<T> enumerator)
        {
            return Wrap(enumerator);

            static async IAsyncEnumerable<JsonElement> Wrap(IAsyncEnumerator<T> enumerator)
            {
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        yield return JsonSerializer.SerializeToElement(enumerator.Current);
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }
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
