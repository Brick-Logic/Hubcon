using Hubcon.Shared.Abstractions.Interfaces;
using System.Net.Http.Headers;

namespace Hubcon.Client.Core.MessageHandlers
{
    public class HttpClientMessageHandler : HttpClientHandler
    {
        private readonly IAuthenticationManager? _authenticationManager;

        public HttpClientMessageHandler(IAuthenticationManager? authenticationManager)
        {
            _authenticationManager = authenticationManager;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_authenticationManager is not null && _authenticationManager.IsSessionActive)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authenticationManager!.AccessToken);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
