using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace Hubcon
{
    public class HubconResponse : HubconResponse<object>, IHubconResponse
    {
        public HubconResponse(bool success, bool failure, string message, string error, int statusCode, object data = null, Exception exception = null) : base(success, failure, message, error, statusCode, data, exception)
        {
        }

        public static HubconResponse Ok(string message = "Success")
        => new HubconResponse(true, false, message, null!, 200);

        public static HubconResponse Ok(object data, string message = "Success")
        => new HubconResponse(true, false, message, null!, 200, data);

        public static HubconResponse Fail(string error, object data = null, Exception exception = null, int statusCode = 400)
            => new HubconResponse(false, true, null!, error, statusCode, data, exception);

        public static HubconResponse Cancelled(Exception exception = null, string error = "Operation cancelled by the user")
            => new HubconResponse(false, true, null!, error, 499, null, exception);

        public static HubconResponse NotFound(Exception exception = null, string error = "Resource not found")
            => new HubconResponse(false, true, null!, error, 404, null, exception);

        public static HubconResponse RequestTooLarge(Exception exception = null, string error = "Request too large")
            => new HubconResponse(false, true, null!, error, 413, null, exception);

        public static HubconResponse TooManyRequests(Exception exception = null, string error = "Too many requests")
            => new HubconResponse(false, true, null!, error, 429, null, exception);

        public static HubconResponse BadRequest(object data = null, Exception exception = null, string error = "Bad request")
            => new HubconResponse(false, true, null!, error, 413, data, exception);

        public static HubconResponse Unauthorized(Exception exception = null, string error = "Unauthorized access")
            => new HubconResponse(false, true, null!, error, 401, null, exception);

        public static HubconResponse Forbidden(Exception exception = null, string error = "Forbidden access")
            => new HubconResponse(false, true, null!, error, 403, null, exception);

        public static HubconResponse InternalError(Exception exception = null, string error = "Internal server error")
            => new HubconResponse(false, true, null!, error, 500, null, exception);


        public static HubconResponse<T> OkT<T>(T data = default, string message = "Success")
            => new HubconResponse<T>(true, false, message, null!, 200, data);

        public static HubconResponse<T> Created<T>(T data, string message = "Created")
            => new HubconResponse<T>(true, false, message, null!, 201, data);

        public static HubconResponse<T> Fail<T>(string error, Exception exception = null, int statusCode = 400, T data = default)
            => new HubconResponse<T>(false, true, null!, error, statusCode, data, exception);

        public static HubconResponse<T> Cancelled<T>(Exception exception = null, string error = "Operation cancelled by the user")
            => new HubconResponse<T>(false, true, null!, error, 499, default!, exception);

        public static HubconResponse<T> NotFound<T>(Exception exception = null, string error = "Resource not found")
            => new HubconResponse<T>(false, true, null!, error, 404, default!, exception);

        public static HubconResponse<T> RequestTooLarge<T>(Exception exception = null, string error = "Request too large")
            => new HubconResponse<T>(false, true, null!, error, 413, default!, exception);

        public static HubconResponse<T> TooManyRequests<T>(Exception exception = null, string error = "Too many requests")
            => new HubconResponse<T>(false, true, null!, error, 429, default!, exception);

        public static HubconResponse<T> Unauthorized<T>(Exception exception = null, string error = "Unauthorized access")
            => new HubconResponse<T>(false, true, null!, error, 401, default!, exception);

        public static HubconResponse<T> Forbidden<T>(Exception exception = null, string error = "Forbidden access")
            => new HubconResponse<T>(false, true, null!, error, 403, default!, exception);

        public static HubconResponse<T> InternalError<T>(Exception exception = null, string error = "Internal server error")
            => new HubconResponse<T>(false, true, null!, error, 500, default!, exception);
    }

    [StructLayout(LayoutKind.Sequential)]
    public class HubconResponse<T> : IHubconResponse<T>
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("data")]
        public T Data { get; set; }

        [JsonIgnore]
        public Exception Exception { get; set; }

        // 2. Tipos de 4 bytes
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        // 3. Tipos de 1 byte (Bools)
        // Al ponerlos al final, el padding necesario para cerrar el objeto es mínimo
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("failure")]
        public bool Failure { get; set; }

        [JsonConstructor]
        public HubconResponse(bool success, bool failure, string message, string error, int statusCode, T data)
        {
            Success = success;
            Failure = failure;
            Message = message;
            Error = error;
            StatusCode = statusCode;
            Data = data;
        }

        internal HubconResponse(bool success, bool failure, string message, string error, int statusCode, T data, Exception exception)
        {
            Success = success;
            Failure = failure;
            Message = message;
            Error = error;
            StatusCode = statusCode;
            Exception = exception;
            Data = data;
        }

        public static implicit operator HubconResponse<T>(T value)
        {
            return HubconResponse.OkT(value);
        }
    }
}