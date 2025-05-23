using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Hubcon.Client.Integration.Client
{
    public class HubconGraphQLClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _httpEndpoint;
        private readonly Uri _wsEndpoint;

        private ClientWebSocket? _webSocket;
        public ClientWebSocket? WebSocket => _webSocket;

        private CancellationTokenSource? _cts;

        private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(15);
        private Task? _receiveLoopTask;

        public HubconGraphQLClient(string httpUrl, string websocketUrl)
        {
            _httpEndpoint = new Uri(httpUrl);
            _wsEndpoint = new Uri(websocketUrl);
            _httpClient = new HttpClient();
        }

        // --- HTTP: Query or Mutation ---
        public async Task<string> SendQueryAsync(string query, object? variables = null, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                query,
                variables
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_httpEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        // --- WebSocket: Subscription ---

        public async Task ConnectWebSocketAsync(CancellationToken cancellationToken = default)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                return;

            _webSocket = new ClientWebSocket();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await _webSocket.ConnectAsync(_wsEndpoint, _cts.Token);

            // Start the receive + ping loop
            _receiveLoopTask = ReceiveLoopAsync(_cts.Token);
            _ = PingLoopAsync(_cts.Token);
        }

        public async Task DisconnectWebSocketAsync()
        {
            if (_webSocket == null)
                return;

            _cts?.Cancel();

            if (_webSocket.State == WebSocketState.Open)
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);

            _webSocket.Dispose();
            _webSocket = null;
        }

        public async Task SendSubscriptionAsync(string subscriptionQuery, object? variables = null, CancellationToken cancellationToken = default)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            var id = Guid.NewGuid().ToString();

            var payload = new
            {
                id,
                type = "start",
                payload = new
                {
                    query = subscriptionQuery,
                    variables
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var buffer = Encoding.UTF8.GetBytes(json);

            await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", cancellationToken);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleWebSocketMessage(message);
                }
            }
            catch (OperationCanceledException) { /* Normal on cancellation */ }
            catch (Exception ex)
            {
                Console.WriteLine("WebSocket receive error: " + ex.Message);
                // Aquí podrías manejar reconexión si querés
            }
        }

        private void HandleWebSocketMessage(string message)
        {
            // Aquí parseás el mensaje según el protocolo GraphQL WS
            // Por ejemplo, mensajes de tipo "data", "error", "complete", "ka" (keep-alive)
            var doc = JsonDocument.Parse(message);
            if (!doc.RootElement.TryGetProperty("type", out var typeElem))
                return;

            var type = typeElem.GetString();

            switch (type)
            {
                case "ka": // keep alive
                           // Recibiste ping del servidor, podrías responder pong o solo ignorar
                    break;

                case "data":
                    // Procesar datos recibidos de la suscripción
                    Console.WriteLine("Received data: " + message);
                    break;

                case "error":
                    Console.WriteLine("Received error: " + message);
                    break;

                case "complete":
                    Console.WriteLine("Subscription complete.");
                    break;

                default:
                    Console.WriteLine("Unknown message type: " + type);
                    break;
            }
        }

        private async Task PingLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                // Enviar ping tipo "ka" (keep-alive)
                var pingPayload = new
                {
                    type = "ka"
                };
                var json = JsonSerializer.Serialize(pingPayload);
                var buffer = Encoding.UTF8.GetBytes(json);

                try
                {
                    await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
                }
                catch
                {
                    // Ignorar errores aquí, el receive loop se encargará de reconectar o cerrar
                }

                await Task.Delay(_pingInterval, cancellationToken);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _webSocket?.Dispose();
            _httpClient.Dispose();
        }
    }

}
