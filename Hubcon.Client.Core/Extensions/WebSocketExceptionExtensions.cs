using System;
using System.Text.RegularExpressions;

namespace Hubcon.Client.Core.Extensions
{
    public static class WebSocketExceptionExtensions
    {
        public static bool TryExtractStatusCode(this Exception ex, out int? value)
        {
            var message = ex.Message;
            value = null;
            
            if (string.IsNullOrEmpty(message)) 
                return false;

            ReadOnlySpan<char> span = message.AsSpan();
            ReadOnlySpan<char> marker = "returned status code '";
        
            var startIndex = span.IndexOf(marker);
            if (startIndex == -1) 
                return false;

            span = span[(startIndex + marker.Length)..];
        
            var endIndex = span.IndexOf('\'');
            if (endIndex == -1) 
                return false;

            var codeSpan = span[..endIndex];

            if (!int.TryParse(codeSpan, out var statusCode)) return false;
            value = statusCode;
            return true;

        }
    }
}