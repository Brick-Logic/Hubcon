using System;
using System.Web;

namespace Hubcon.Client.Core.Extensions
{
    public static class UriExtensions
    {
        public static Uri AddQueryParameter(this UriBuilder uriBuilder, string key, string value)
        {
            // Parse existing query (sin el signo ?)
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[key] = value;

            // Asignar la nueva query
            uriBuilder.Query = query.ToString(); // ya codifica automáticamente

            return uriBuilder.Uri;
        }
    }
}
