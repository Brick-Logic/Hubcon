using Hubcon.Core.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Middlewares.MessageHandlers
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
