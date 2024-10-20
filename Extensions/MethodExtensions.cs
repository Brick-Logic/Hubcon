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
            List<string> identifiers = [];
            identifiers.Add(method.ReturnType.Name);
            identifiers.Add(method.Name);
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

        public static object[] ConvertArgsToDelegateTypes(Delegate del, object[] args)
        {
            var methodParams = del.Method.GetParameters();

            if (args.Length == 0)
                return [];

            if (args.Length + 1 != methodParams.Length)
                throw new ArgumentException("El número de argumentos no coincide con los parámetros del método");

            object[] convertedArgs = new object[args.Length];
            for (int i = 0; i < methodParams.Length - 1; i++)
            {
                var expectedType = methodParams[i+1].ParameterType;

                if (args[i] != null && !expectedType.IsAssignableFrom(args[i].GetType()))
                {
                    // Convertir el argumento si es necesario
                    convertedArgs[i] = JsonElementParser.ConvertJsonElement(args[i], expectedType)!;
                }
                else
                {
                    // Si es null o ya es del tipo correcto, se usa directamente
                    convertedArgs[i] = args[i];
                }
            }

            // Invocar el delegado con los argumentos convertidos
            return convertedArgs;
        }
    }
}
