using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.RateLimiting;
using Hubcon.Server.Core.Routing.Registries;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Websockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Routing.Models
{
    public class SseResult : IResult
    {
        private readonly IAsyncEnumerable<object?> _stream;
        private readonly IOperationRequest request;

        public SseResult(IAsyncEnumerable<object?> stream, IOperationRequest request)
        {
            _stream = stream;
            this.request = request;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            IRateLimiterManager rateLimiter = null!;
            var id = Guid.NewGuid();
            PipeWriter writer = null!;
            try
            {
                var response = httpContext.Response;
                var services = httpContext.RequestServices;

                // 1. Configuración de Headers obligatorios
                response.ContentType = "text/event-stream";
                response.Headers.CacheControl = "no-cache";
                response.Headers.Connection = "keep-alive";
                response.Headers["X-Accel-Buffering"] = "no";

                rateLimiter = services.GetRequiredService<IRateLimiterManager>();

                await rateLimiter.Link(id, HttpAttribute.Default, request);

                // 2. Usamos el BodyWriter para máxima performance (Zero-copy)
                writer = response.BodyWriter;
                var converter = httpContext.RequestServices.GetRequiredService<IDynamicConverter>();
                await foreach (var item in _stream.WithCancellation(httpContext.RequestAborted))
                {
                    await rateLimiter.TryAcquireAsync(MessageType.stream_data, id);

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
                await response.WriteAsync("[DONE]\n\n");
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                if (rateLimiter != null)
                    await rateLimiter.Unlink(id);
            }
        }
    }
}
