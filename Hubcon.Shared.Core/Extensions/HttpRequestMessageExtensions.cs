using System;
using System.Net.Http;

namespace Hubcon.Shared.Core.Extensions
{
    public static class HttpRequestMessageExtensions
    {
        public static HttpRequestMessage SetBrowserResponseStreamingEnabled(this HttpRequestMessage requestMessage, bool streamingEnabled)
        {
            if(requestMessage == null) throw new ArgumentNullException(nameof(requestMessage));
            requestMessage.Properties["WebAssemblyEnableStreamingResponse"] = streamingEnabled;
            return requestMessage;
        }
    }
}
