using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    public static class Response
    {
        // Create
        public static IOperationResponse<T> Create<T>(bool success) => new BaseOperationResponse<T>(success, default!, null);
        public static IOperationResponse<T> Create<T>(bool success, T? data) => new BaseOperationResponse<T>(success, data!, null);
        public static IOperationResponse<T> Create<T>(bool success, T? data, string error) => new BaseOperationResponse<T>(success, data!, error);

        // OK
        public static IOperationResult Ok() => new BaseOperationResponse<object>(true, default!, null);
        public static IOperationResponse<T> Ok<T>(T? data) => new BaseOperationResponse<T>(true, data!, null);
        public static IOperationResponse<JsonElement> Ok(JsonElement data) => new BaseJsonResponse(true, data, null);     
        

        // Error
        public static IOperationResult Error() => new BaseOperationResponse<object>(false, default!, "Unkown error");
        public static IOperationResult Error(string error) => new BaseOperationResponse<object>(false, default!, error);


        // Unauthorized
        public static IOperationResult Unauthorized() => new BaseOperationResponse<object>(false, default!, "Unauthorized");
        public static IOperationResult Unauthorized(string error) => new BaseOperationResponse<object>(false, default!, error);

        // Bad requestdefault!
        public static IOperationResult BadRequest() => new BaseOperationResponse<object>(false, default!, "Bad request");
        public static IOperationResult BadRequest(string error) => new BaseOperationResponse<object>(false, default!, error);

        // Not found
        public static IOperationResult NotFound() => new BaseOperationResponse<object>(false, default!, "Not found");
        public static IOperationResult NotFound(string error) => new BaseOperationResponse<object>(false, default!, error);
    }
}
