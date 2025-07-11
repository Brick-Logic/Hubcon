using Hubcon.Shared.Abstractions.Interfaces;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;


namespace Hubcon.Shared.Core.Serialization
{
    public static class DataSerializer
    {
        // Buffers fijos reutilizables
        private static readonly byte[] SerializationBuffer = new byte[8192];
        private static readonly byte[] DeserializationBuffer = new byte[8192];

        // Reutilizable para UTF8 encoding/decoding
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Serializa un objeto de tipo T y lo envía por WebSocket con allocs mínimos
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a serializar</typeparam>
        /// <param name="webSocket">WebSocket para enviar los datos</param>
        /// <param name="data">Objeto a serializar</param>
        /// <param name="cancellationToken">Token de cancelación</param>
        /// <returns>Task que representa la operación asíncrona</returns>
        public static async Task SerializeAndSendAsync<T>(
            WebSocket webSocket,
            T data,
            IDynamicConverter converter,
            CancellationToken cancellationToken = default)
        {
            // Usar Utf8JsonWriter directamente sobre el buffer para evitar string intermedio
            var writer = new Utf8JsonWriter(new MemoryStream(SerializationBuffer));

            try
            {
                // Serializar directamente a UTF-8 bytes sin crear string intermedio
                JsonSerializer.Serialize(writer, data, converter.JsonSerializerOptions);
                await writer.FlushAsync(cancellationToken);

                int bytesWritten = (int)writer.BytesCommitted;

                // Verificar que no exceda el tamaño del buffer
                if (bytesWritten > SerializationBuffer.Length)
                {
                    throw new InvalidOperationException(
                        $"El objeto serializado ({bytesWritten} bytes) excede el tamaño del buffer ({SerializationBuffer.Length} bytes)");
                }

                // Enviar directamente desde el buffer sin copiar
                await webSocket.SendAsync(
                    new ArraySegment<byte>(SerializationBuffer, 0, bytesWritten),
                    WebSocketMessageType.Binary,
                    true,
                    cancellationToken);
            }
            finally
            {
                await writer.DisposeAsync();
            }
        }

        /// <summary>
        /// Recibe datos por WebSocket y los deserializa al tipo T con allocs mínimos
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a deserializar</typeparam>
        /// <param name="webSocket">WebSocket para recibir los datos</param>
        /// <param name="cancellationToken">Token de cancelación</param>
        /// <returns>Objeto deserializado de tipo T</returns>
        public static async Task<byte[]> ReceiveAndDeserializeAsync(
            WebSocket webSocket,
            CancellationToken cancellationToken = default)
        {
            // Recibir datos directamente en el buffer fijo
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(DeserializationBuffer),
                cancellationToken);

            // Verificar que sea un mensaje de texto completo
            //if (result.MessageType != WebSocketMessageType.Text)
            //{
            //    throw new InvalidOperationException("Se esperaba un mensaje de texto");
            //}

            if (!result.EndOfMessage)
            {
                throw new InvalidOperationException("El mensaje no cabe en el buffer o está fragmentado");
            }

            // Crear ArraySegment desde el buffer para evitar copias
            var jsonBytes = new ArraySegment<byte>(DeserializationBuffer, 0, result.Count);

            // Usar MemoryStream sobre el buffer para deserializar sin crear string
            using var memoryStream = new MemoryStream(DeserializationBuffer, 0, result.Count, false);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Versión alternativa usando MemoryStream sobre buffer fijo para mejor rendimiento
        /// </summary>
        public static async Task SerializeAndSendMemoryStreamAsync<T>(
            WebSocket webSocket,
            T data,
            CancellationToken cancellationToken = default)
        {
            // Usar MemoryStream sobre el buffer fijo existente
            using var memoryStream = new MemoryStream(SerializationBuffer);
            using var writer = new Utf8JsonWriter(memoryStream);

            JsonSerializer.Serialize(writer, data);
            await writer.FlushAsync(cancellationToken);

            int bytesWritten = (int)memoryStream.Position;

            if (bytesWritten > SerializationBuffer.Length)
            {
                throw new InvalidOperationException(
                    $"El objeto serializado ({bytesWritten} bytes) excede el tamaño del buffer ({SerializationBuffer.Length} bytes)");
            }

            await webSocket.SendAsync(
                new ArraySegment<byte>(SerializationBuffer, 0, bytesWritten),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }
    }
}
