using Hubcon.Core.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Core.Invocation
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
        public static IMethodResponse<JsonElement> Ok(JsonElement data) => new BaseJsonResponse(true, data, null);     
        

        // Error
        public static IMethodResponse Error() => new BaseMethodResponse(false, null, "Unkown error");
        public static IMethodResponse Error(string error) => new BaseMethodResponse(false, null, error);


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
