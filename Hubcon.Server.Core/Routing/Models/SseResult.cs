using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Routing.Models
{
    public class SseResult : IResult
    {
        private readonly IAsyncEnumerable<object?> _stream;

        public SseResult(IAsyncEnumerable<object?> stream)
        {
            _stream = stream;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;

            // 1. Configuración de Headers obligatorios
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers.Connection = "keep-alive";

            // 2. Usamos el BodyWriter para máxima performance (Zero-copy)
            var writer = response.BodyWriter;
            var converter = httpContext.RequestServices.GetRequiredService<IDynamicConverter>();
            try
            {
                await foreach (var item in _stream.WithCancellation(httpContext.RequestAborted))
                {
                    if (item is null) continue;

                    // 3. Formateamos el mensaje: data: {json}\n\n
                    var json = converter.Serialize(item);
                    var message = $"data: {json}\n\n";

                    // 4. Escribimos directamente en el buffer de red
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await writer.WriteAsync(bytes, httpContext.RequestAborted);

                    // 5. El Flush es clave para que el cliente vea el token YA
                    await response.Body.FlushAsync(httpContext.RequestAborted);
                }

                // Opcional: Mandar el [DONE] para cerrar el ciclo
                await response.WriteAsync("data: [DONE]\n\n");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
