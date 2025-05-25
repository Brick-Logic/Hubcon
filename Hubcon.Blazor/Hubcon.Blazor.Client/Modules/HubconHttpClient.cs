using Hubcon.Blazor.Client.Auth;
using Hubcon.Client.Core.MessageHandlers;

namespace Hubcon.Blazor.Client.Modules
{
    public class HubconHttpClient : HttpClient
    {
        public HubconHttpClient(AuthenticationManager authenticationManager) : base(new HttpClientMessageHandler(authenticationManager))
        {
        }

        public new async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
        {
            // Interceptar acá
            Console.WriteLine($"Sending: {request.RequestUri}");

            // Headers, tokens, logging, etc.
            return await base.SendAsync(request, token);
        }
    }
}
