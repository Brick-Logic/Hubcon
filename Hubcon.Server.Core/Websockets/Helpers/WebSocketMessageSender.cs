using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Serialization;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    internal sealed class WebSocketMessageSender(WebSocket webSocket, IDynamicConverter converter)
    {
        public WebSocketState State => webSocket.State;

        public async Task SendAsync<T>(T message) where T : BaseMessage
        {
            try
            {
                if (webSocket?.State != WebSocketState.Open) 
                    return;
                
                var pipe = new Pipe();
                var writer = new Utf8JsonWriter(pipe.Writer);

                converter.Serialize(writer, message);
                
                await writer.FlushAsync();
                await pipe.Writer.CompleteAsync();

                var result = await pipe.Reader.ReadAsync();
                var buffer = result.Buffer;

                var bytes = buffer.ToArray();
                await pipe.Reader.CompleteAsync();

                await webSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            finally
            {
                message.Dispose();
            }
        }
    }
}