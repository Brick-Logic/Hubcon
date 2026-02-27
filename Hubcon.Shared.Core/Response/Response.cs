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
        public HubconResponse(bool success, bool failure, string message, string error, int statusCode, object data = null, object originalResponse = null, Exception exception = null)
            : base(success, failure, message, error, statusCode, data, originalResponse, exception)
        {
        }

        public static HubconResponse Ok(string message = "Success", object originalData = null)
        => new HubconResponse(true, false, message, null!, 200, null, originalData);

        public static HubconResponse Ok(object data, string message = "Success", object originalData = null)
        => new HubconResponse(true, false, message, null!, 200, data, originalData);

        public static HubconResponse Fail(string error, object data = null, Exception exception = null, int statusCode = 400, object originalData = null)
            => new HubconResponse(false, true, null!, error, statusCode, data, originalData, exception);

        public static HubconResponse Cancelled(Exception exception = null, string error = "Operation cancelled by the user", object originalData = null)
            => new HubconResponse(false, true, null!, error, 499, null, originalData, exception);

        public static HubconResponse NotFound(Exception exception = null, string error = "Resource not found", object originalData = null)
            => new HubconResponse(false, true, null!, error, 404, null, originalData, exception);

        public static HubconResponse RequestTooLarge(Exception exception = null, string error = "Request too large", object originalData = null)
            => new HubconResponse(false, true, null!, error, 413, null, originalData, exception);

        public static HubconResponse TooManyRequests(Exception exception = null, string error = "Too many requests", object originalData = null)
            => new HubconResponse(false, true, null!, error, 429, null, originalData, exception);

        public static HubconResponse BadRequest(object data = null, Exception exception = null, string error = "Bad request", object originalData = null)
            => new HubconResponse(false, true, null!, error, 413, data, originalData, exception);

        public static HubconResponse Unauthorized(Exception exception = null, string error = "Unauthorized access", object originalData = null)
            => new HubconResponse(false, true, null!, error, 401, null, originalData, exception);

        public static HubconResponse Forbidden(Exception exception = null, string error = "Forbidden access", object originalData = null)
            => new HubconResponse(false, true, null!, error, 403, null, originalData, exception);

        public static HubconResponse InternalError(Exception exception = null, string error = "Internal server error", object originalData = null)
            => new HubconResponse(false, true, null!, error, 500, null, originalData, exception);


        public static HubconResponse<T> OkT<T>(T data = default, string message = "Success", object originalData = null)
            => new HubconResponse<T>(true, false, message, null!, 200, data, originalData, null);

        public static HubconResponse<T> Created<T>(T data, string message = "Created", object originalData = null)
            => new HubconResponse<T>(true, false, message, null!, 201, data, originalData, null);

        public static HubconResponse<T> Fail<T>(string error, Exception exception = null, int statusCode = 400, T data = default, object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, statusCode, data, originalData, exception);

        public static HubconResponse<T> Cancelled<T>(Exception exception = null, string error = "Operation cancelled by the user", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 499, default!, originalData, exception);

        public static HubconResponse<T> NotFound<T>(Exception exception = null, string error = "Resource not found", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 404, default!, originalData, exception);

        public static HubconResponse<T> RequestTooLarge<T>(Exception exception = null, string error = "Request too large", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 413, default!, originalData, exception);

        public static HubconResponse<T> BadRequest<T>(object data = null, Exception exception = null, string error = "Bad request", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 413, default!, originalData, exception);

        public static HubconResponse<T> TooManyRequests<T>(Exception exception = null, string error = "Too many requests", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 429, default!, originalData, exception);

        public static HubconResponse<T> Unauthorized<T>(Exception exception = null, string error = "Unauthorized access", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 401, default!, originalData, exception);

        public static HubconResponse<T> Forbidden<T>(Exception exception = null, string error = "Forbidden access", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 403, default!, originalData, exception);

        public static HubconResponse<T> InternalError<T>(Exception exception = null, string error = "Internal server error", object originalData = null)
            => new HubconResponse<T>(false, true, null!, error, 500, default!, originalData, exception);
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
        public object OriginalResponse { get; }

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

        public HubconResponse(bool success, bool failure, string message, string error, int statusCode, T data, object originalResponse)
        {
            Success = success;
            Failure = failure;
            Message = message;
            Error = error;
            StatusCode = statusCode;
            Data = data;
            OriginalResponse = originalResponse;
        }

        internal HubconResponse(bool success, bool failure, string message, string error, int statusCode, T data, object originalResponse, Exception exception)
        {
            Success = success;
            Failure = failure;
            Message = message;
            Error = error;
            StatusCode = statusCode;
            Exception = exception;
            Data = data;
            OriginalResponse = originalResponse;
        }

        public static implicit operator HubconResponse<T>(T value)
        {
            return HubconResponse.OkT(value);
        }

        public IHubconResponse GetBoxed()
        {
            return new HubconResponse(Success, Failure, Message, Error, StatusCode, Data, Exception);
        }
    }
}