using Hubcon.Core.Converters;
using Hubcon.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;

namespace Hubcon.SignalR.Extensions
{
    internal static class HubConnectionExtensions
    {
        public static async Task<IAsyncEnumerable<T>> StreamAsync<T>(this HubConnection connection, MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            var stream = connection.StreamAsync<object>(request.HandlerMethodName!, request, cancellationToken);

            return await Task.FromResult(ConvertStream<T>(stream, cancellationToken));
        }

        private static async IAsyncEnumerable<T> ConvertStream<T>(IAsyncEnumerable<object> stream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                if (item is T typedItem)
                {
                    yield return typedItem;
                }
                else
                {
                    yield return DynamicConverter.DeserializeData<T>(item)!;
                }
            }
        }
    }
}
