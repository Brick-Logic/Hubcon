using Hubcon.JsonElementTools;
using Hubcon.Models;
using Hubcon.Response;
using Microsoft.AspNetCore.SignalR;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Extensions
{
    public static class HubconExtensions
    {
        public static string GetMethodSignature(this MethodInfo method)
        {
            List<string> identifiers = [method.Name];
            identifiers.AddRange(method.GetParameters().Select(p => p.ParameterType.Name));
            var result = string.Join("_", identifiers);
            return result;
        }

        public static async Task<T?> InvokeMethodAsync<T>(this ISingleClientProxy client, string method, CancellationToken cancellationToken = default, params object?[] args)
        {
            object request = new MethodInvokeInfo(method, args);
            MethodResponse result = await client.InvokeAsync<MethodResponse>(method, request, cancellationToken);
            return (T?)Convert.ChangeType(JsonElementParser.ConvertJsonElement<T>((JsonElement)result.Data!), typeof(T?));
        }

        public static async Task<MethodResponse> InvokeMethodAsync(this ISingleClientProxy client, string method, CancellationToken cancellationToken = default, params object?[] args)
        {
            object request = new MethodInvokeInfo(method, args);
            return await client.InvokeAsync<MethodResponse>(method, request, cancellationToken);
        }

        public static async Task CallMethodAsync(this ISingleClientProxy client, string method, CancellationToken cancellationToken = default, params object?[] args)
        {
            object request = new MethodInvokeInfo(method, args);
            await client.SendAsync(method, request, cancellationToken);
        }
    }
}
