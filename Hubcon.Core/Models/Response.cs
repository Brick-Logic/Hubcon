using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Core.Models
{
    public static class Response
    {
        // Create
        public static IMethodResponse<T> Create<T>(bool success) => new BaseMethodResponse<T>(success, default, null);
        public static IMethodResponse<T> Create<T>(bool success, T? data) => new BaseMethodResponse<T>(success, data, null);
        public static IMethodResponse<T> Create<T>(bool success, T? data, string error) => new BaseMethodResponse<T>(success, data, error);

        // OK
        public static IMethodResponse Ok() => new BaseMethodResponse(true, null, null);
        public static IMethodResponse<T> Ok<T>(T? data) => new BaseMethodResponse<T>(true, data, null);
        public static IMethodResponse<JsonElement> Ok(JsonElement data) => (IMethodResponse<JsonElement>)new BaseJsonResponse(true, data, null);     
        

        // Error
        public static IMethodResponse Error() => new BaseMethodResponse(false, null, null);
        public static IMethodResponse Error(string error) => new BaseMethodResponse(false, null, error);
        public static IMethodResponse<T> Error<T>(string error) => new BaseMethodResponse<T>(false, default, error);


        // Unauthorized
        public static IMethodResponse Unauthorized() => new BaseMethodResponse(false, null, "Unauthorized");
        public static IMethodResponse Unauthorized(string error) => new BaseMethodResponse(false, null, error);

        // Bad request
        public static IMethodResponse BadRequest() => new BaseMethodResponse(false, null, "Bad request");
        public static IMethodResponse BadRequest(string error) => new BaseMethodResponse(false, null, error);

        // Not found
        public static IMethodResponse NotFound() => new BaseMethodResponse(false, null, "Not found");
        public static IMethodResponse NotFound(string error) => new BaseMethodResponse(false, null, error);

    }
}
