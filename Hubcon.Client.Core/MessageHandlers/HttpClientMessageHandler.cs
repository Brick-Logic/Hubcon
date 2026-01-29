using Hubcon.Shared.Abstractions.Interfaces;
using System.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;

namespace Hubcon.Client.Core.MessageHandlers
{
    internal sealed class HttpClientMessageHandler : HttpClientHandler
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
