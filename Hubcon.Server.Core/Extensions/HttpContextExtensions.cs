using Hubcon.Server.Core.Routing.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<JsonReadResult> TryReadJsonAsync(this HttpContext context)
        {
            return await ReadBodyAsJsonElementAsync(context);
        }

        public static async Task<JsonReadResult> ReadBodyAsJsonElementAsync(HttpContext context)
        {
            try
            {
                // Verificar que el content type sea JSON
                if (!IsJsonContentType(context.Request.ContentType))
                {
                    return JsonReadResult.Failure("Content-Type debe ser application/json");
                }

                // Verificar que haya contenido
                if (context.Request.ContentLength == 0)
                {
                    return JsonReadResult.Failure("El body de la request está vacío");
                }

                //context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

                var bodyText = await reader.ReadToEndAsync();

                // Verificar que el contenido no esté vacío después de leerlo
                if (string.IsNullOrWhiteSpace(bodyText))
                {
                    context.Request.Body.Position = 0;
                    return JsonReadResult.Failure("El contenido del body está vacío o solo contiene espacios en blanco");
                }

                // Intentar parsear el JSON
                try
                {
                    using var doc = JsonDocument.Parse(bodyText, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 64 // Limitar profundidad para evitar ataques
                    });

                    context.Request.Body.Position = 0;
                    return JsonReadResult.Success(doc.RootElement.Clone());
                }
                catch (JsonException ex)
                {
                    context.Request.Body.Position = 0;
                    return JsonReadResult.Failure($"JSON inválido: {ex.Message}");
                }
            }
            catch (IOException ex)
            {
                return JsonReadResult.Failure($"Error al leer el stream: {ex.Message}");
            }
            catch (Exception ex)
            {
                return JsonReadResult.Failure($"Error inesperado: {ex.Message}");
            }
        }

        private static bool IsJsonContentType(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            // Extraer solo el media type, ignorando charset y otros parámetros
            var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();

            return mediaType == "application/json" ||
                   mediaType == "application/json-patch+json" ||
                   mediaType == "text/json";
        }
    }


}
