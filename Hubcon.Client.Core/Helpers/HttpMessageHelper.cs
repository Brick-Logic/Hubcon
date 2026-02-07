using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace Hubcon.Client.Core.Helpers
{
    internal static class HttpMessageHelper
    {
        static ConcurrentDictionary<MethodInfo, string> metadata = new();

        public static string BuildBodyAndFinalUrl(
            IOperationRequest request, 
            IClientOperationContext context, 
            string finalRoute, 
            Dictionary<string, object> remainingArguments, 
            ref StringContent? content)
        {
            string url;

            if (context.Member is not MethodInfo)
                throw new InvalidOperationException("This method can only be used by a method context.");

            var methodInfo = context.Member as MethodInfo;

            var uriBuilder = new UriBuilder(context.Uri);
            uriBuilder.Scheme = context.UseSecureConnection ? "https" : "http";
            url = uriBuilder.ToString();

            // 3. Construcción de Body o QueryString según el Verbo
            if (context.HttpMethodDefined == HttpMethod.Post || context.HttpMethodDefined == HttpMethod.Put)
            {
                object? bodyData = null;

                // Intentamos obtener el nombre del parámetro marcado con [Body]
                var bodyParamName = metadata.GetOrAdd(methodInfo!, method =>
                    method.GetParameters()
                          .FirstOrDefault(p => p.GetCustomAttribute<AsBodyAttribute>() != null)?.Name!);

                // Si existe un parámetro [Body] y está en los argumentos, lo extraemos (Aplanamiento)
                if (bodyParamName != null && request.Arguments.TryGetValue(bodyParamName, out var explicitBody))
                {
                    bodyData = explicitBody;
                }
                else
                {
                    // Lógica original: Solo enviamos en el Body lo que NO se usó en la URL
                    // Si queda solo un argumento llamado "value", lo desempaquetamos.
                    // Si no, enviamos el diccionario con lo restante.
                    bodyData = remainingArguments.Count == 1 && remainingArguments.ContainsKey("value")
                                ? remainingArguments["value"]
                                : remainingArguments;
                }

                var jsonBody = context.Converter.Serialize(bodyData);
                content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                url = url.TrimEnd('/') + "/" + finalRoute.TrimStart('/');
            }
            else // GET o DELETE
            {
                var builder = new UriBuilder(url);
                builder.Path = (builder.Path.TrimEnd('/') + "/" + finalRoute.TrimStart('/')).Replace("//", "/");
                var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                // Intentamos obtener el nombre del parámetro marcado con [AsQuery]
                var queryParamName = metadata.GetOrAdd(methodInfo, method =>
                    method.GetParameters()
                          .FirstOrDefault(p => p.GetCustomAttribute<AsQueryAttribute>() != null)?.Name!);

                // Si hay un objeto [AsQuery], lo aplanamos
                if (queryParamName != null && remainingArguments.TryGetValue(queryParamName, out var queryObj) && queryObj != null)
                {
                    // Usamos reflexión (o podrías usar el converter si tiene un ToDictionary) 
                    // para extraer las propiedades del objeto a la QueryString
                    var props = queryObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        var val = prop.GetValue(queryObj);
                        if (val != null) query[prop.Name] = val.ToString();
                    }

                    // Removemos el objeto original de los restantes para que no se duplique
                    remainingArguments.Remove(queryParamName);
                }

                // El resto de argumentos sobrantes van como parámetros normales
                foreach (var arg in remainingArguments)
                {
                    query[arg.Key] = arg.Value?.ToString() ?? "";
                }

                builder.Query = query.ToString();
                url = builder.ToString();
            }

            return url;
        }

        public static Dictionary<string, object> GetRemainingArguments(IOperationRequest request, IDynamicConverter converter, ref string finalRoute)
        {

            // 2. Lógica de Reemplazo en URL (Path Parameters)
            // Copiamos los argumentos a una lista de trabajo para saber cuáles sobran (y van al Body o Query)
            var remainingArguments = request.Arguments.ToDictionary(k => k.Key, v => v.Value);

            foreach (var arg in request.Arguments)
            {
                string placeholder = $"{{{arg.Key}}}";
                if (finalRoute.Contains(placeholder))
                {
                    string? value = null;

                    if (arg.Value is Enum)
                    {
                        value = converter.SerializeToElement(arg.Value).ToString();
                    }

                    value ??= Uri.EscapeDataString(arg.Value?.ToString() ?? "");
                    finalRoute = finalRoute.Replace(placeholder, value);
                    remainingArguments.Remove(arg.Key); // Ya se usó en el Path, lo quitamos
                }
            }

            return remainingArguments;
        }

        public static async IAsyncEnumerable<JsonElement> ParseSSEStream(HttpResponseMessage response, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var dataMessages = context.Attributes.OfType<ParseSseMessageAttribute>();
            var endMessages = context.Attributes.OfType<ParseEndSseMessageAttribute>().Select(x => x.MessageName);
            bool shouldReadRaw = context.Attributes.Any(x => x is ParseRawSseMessageAttribute);
            var converter = context.Converter;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            string line = "";
            ParseSseMessageAttribute? foundMessage = null;

            while (!cancellationToken.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
            {
                if (endMessages.Any() && endMessages.Any(x => line.StartsWith(x)))
                    break;

                if (dataMessages.Any())
                    foundMessage = dataMessages.FirstOrDefault(x => line.StartsWith(x.MessageName))!;

                if (foundMessage != null)
                {
                    var sliced = line.Substring(foundMessage.MessageName.Length);
                    var parsed = !string.IsNullOrWhiteSpace(foundMessage.JsonPropertyName)
                        ? WrapInObject(foundMessage.JsonPropertyName, sliced).ToJsonString()
                        : sliced;
                    JsonElement ev;
                    try
                    {
                        ev = JsonElement.Parse(parsed);
                    }
                    catch
                    {
                        ev = converter.SerializeToElement(parsed);
                    }

                    if (ev.ValueKind != JsonValueKind.Null) yield return ev;
                    foundMessage = null;
                }
                else if (!string.IsNullOrWhiteSpace(line) && shouldReadRaw)
                {
                    var ev = converter.ToJsonElement(line);
                    if (ev.ValueKind != JsonValueKind.Null) yield return ev;
                }
                else if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("data:"))
                {
                    var sliced = line.Substring(6);
                    var ev = converter.SerializeToElement(sliced);
                    if (ev.ValueKind != JsonValueKind.Null) yield return ev;
                }
            }

            stream.Dispose();
        }

        private static JsonObject WrapInObject(string title, string rawInput)
        {
            var root = new JsonObject();

            try
            {
                var node = JsonNode.Parse(rawInput);
                root[title] = node;
            }
            catch (JsonException)
            {
                root[title] = JsonValue.Create(rawInput);
            }

            return root;
        }

    }
}
