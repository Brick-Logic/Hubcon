using Hubcon.Core.Converters;
using Hubcon.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.SignalR.Extensions
{
    internal static class HubConnectionExtensions
    {
        public static async Task<IAsyncEnumerable<T>> StreamAsync<T>(
            this HubConnection connection, 
            MethodInvokeRequest request, 
            DynamicConverter converter, 
            CancellationToken cancellationToken)
        {
            var stream = connection.StreamAsync<JsonElement?>(request.HandlerMethodName!, request, cancellationToken);

            return await Task.FromResult(converter.ConvertStream<T>(stream, converter, cancellationToken));
        }
    }
}
