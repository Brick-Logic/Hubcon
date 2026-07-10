using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace Hubcon
{
    /// <summary>
    /// Represents a non-generic response envelope for Hubcon operations.
    /// Provides a set of static factory methods to create standard success and error responses.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class HubconResponse : HubconResponse<object>, IHubconResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HubconResponse"/> class.
        /// </summary>
        public HubconResponse(bool success, bool failure, string message, string error, int statusCode, object data = null!, object originalResponse = null!, Exception exception = null!)
            : base(success, failure, message, error, statusCode, data, originalResponse, exception)
        {
        }

        /// <summary>Creates a successful response (200 OK).</summary>
        public static HubconResponse Ok(string message = "Success", object originalData = null!)
            => new HubconResponse(true, false, message, null!, 200, null!, originalData);

        /// <summary>Creates a successful response (200 OK) with the specified data.</summary>
        public static HubconResponse Ok(object data, string message = "Success", object originalData = null!)
            => new HubconResponse(true, false, message, null!, 200, data, originalData);

        /// <summary>Creates a failure response (400 Bad Request by default!).</summary>
        public static HubconResponse Fail(string error, object data = null!, Exception exception = null!, int statusCode = 400, object originalData = null!)
            => new HubconResponse(false, true, null!, error, statusCode, data, originalData, exception);

        /// <summary>Creates a response indicating the operation was cancelled (499 Client Closed Request).</summary>
        public static HubconResponse Cancelled(Exception exception = null!, string error = "Operation cancelled by the user", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 499, null!, originalData, exception);

        /// <summary>Creates a response indicating the resource was not found (404 Not Found).</summary>
        public static HubconResponse NotFound(Exception exception = null!, string error = "Resource not found", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 404, null!, originalData, exception);

        /// <summary>Creates a response indicating the request entity is too large (413 Payload Too Large).</summary>
        public static HubconResponse RequestTooLarge(Exception exception = null!, string error = "Request too large", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 413, null!, originalData, exception);

        /// <summary>Creates a response indicating rate limiting has been exceeded (429 Too Many Requests).</summary>
        public static HubconResponse TooManyRequests(Exception exception = null!, string error = "Too many requests", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 429, null!, originalData, exception);

        /// <summary>Creates a generic bad request response (400 Bad Request).</summary>
        public static HubconResponse BadRequest(object data = null!, Exception exception = null!, string error = "Bad request", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 400, data, originalData, exception);

        /// <summary>Creates a response indicating the request requires authentication (401 Unauthorized).</summary>
        public static HubconResponse Unauthorized(Exception exception = null!, string error = "Unauthorized access", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 401, null!, originalData, exception);

        /// <summary>Creates a response indicating the user does not have permission (403 Forbidden).</summary>
        public static HubconResponse Forbidden(Exception exception = null!, string error = "Forbidden access", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 403, null!, originalData, exception);

        /// <summary>Creates a response indicating an unexpected server-side error (500 Internal Server Error).</summary>
        public static HubconResponse InternalError(Exception exception = null!, string error = "Internal server error", object originalData = null!)
            => new HubconResponse(false, true, null!, error, 500, null!, originalData, exception);

        /// <summary>Creates a successful generic response (200 OK) for a specific type <typeparamref name="T"/>.</summary>
        public static HubconResponse<T> OkT<T>(T data = default!, string message = "Success", object originalData = null!)
            => new HubconResponse<T>(true, false, message, null!, 200, data, originalData, null!);

        /// <summary>Creates a successful response indicating a resource was created (201 Created).</summary>
        public static HubconResponse<T> Created<T>(T data, string message = "Created", object originalData = null!)
            => new HubconResponse<T>(true, false, message, null!, 201, data, originalData, null!);

        /// <summary>Creates a typed failure response.</summary>
        public static HubconResponse<T> Fail<T>(string error, Exception exception = null!, int statusCode = 400, T data = default!, object originalData = null!)
            => new HubconResponse<T>(false, true, null!, error, statusCode, data, originalData, exception);

        /// <summary>Creates a typed cancellation response.</summary>
        public static HubconResponse<T> Cancelled<T>(Exception exception = null!, string error = "Operation cancelled by the user", object originalData = null!)
            => new HubconResponse<T>(false, true, null!, error, 499, default!, originalData, exception);

        /// <summary>Creates a typed not found response.</summary>
        public static HubconResponse<T> NotFound<T>(Exception exception = null!, string error = "Resource not found", object originalData = null!)
            => new HubconResponse<T>(false, true, null!, error, 404, default!, originalData, exception);

        /// <summary>Creates a typed internal error response.</summary>
        public static HubconResponse<T> InternalError<T>(Exception exception = null!, string error = "Internal server error", object originalData = null!)
            => new HubconResponse<T>(false, true, null!, error, 500, default!, originalData, exception);

        /// <summary>
        /// Implicitly converts an exception into an error response contained in <see cref="HubconResponse"/>.
        /// </summary>
        /// <param name="value">The data to wrap.</param>
        public static implicit operator HubconResponse(Exception value)
        {
            if (value is OperationCanceledException)
                return HubconResponse.Cancelled(null!, value.Message);
            else
                return HubconResponse.InternalError(value);
        }
    }

    /// <summary>
    /// Represents a generic response envelope that carries data of a specific type <typeparamref name="T"/>.
    /// Optimized for memory layout and serialization.
    /// </summary>
    /// <typeparam name="T">The type of the data being returned.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public class HubconResponse<T> : IHubconResponse<T>
    {
        /// <summary>Gets or sets the payload of the response.</summary>
        [JsonPropertyName("data")]
        public T Data { get; set; }
        
        /// <summary>Gets or sets the protocol-specific status code.</summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }
        
        /// <summary>Gets a value indicating whether the operation was successful.</summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>Gets a value indicating whether the operation failed.</summary>
        [JsonPropertyName("failure")]
        public bool Failure { get; set; }
        
        /// <summary>Gets or sets the error message, if any.</summary>
        [JsonPropertyName("error")]
        public string Error { get; set; }
        
        /// <summary>Gets or sets the success message.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        /// <summary>Gets the raw, original response object from the transport layer.</summary>
        [JsonIgnore]
        public object? OriginalResponse { get; }

        /// <summary>Gets or sets the exception associated with a failure, if applicable.</summary>
        [JsonIgnore]
        public Exception? Exception { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="HubconResponse{T}"/> class for serialization.
        /// </summary>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="HubconResponse{T}"/> class with an original response.
        /// </summary>
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

        /// <summary>
        /// Implicitly converts a value of type <typeparamref name="T"/> into a successful <see cref="HubconResponse{T}"/>.
        /// </summary>
        /// <param name="value">The data to wrap.</param>
        public static implicit operator HubconResponse<T>(T value)
        {
            return HubconResponse.OkT(value);
        }

        /// <summary>
        /// Implicitly converts an exception into an error response contained in <see cref="HubconResponse{T}"/>.
        /// </summary>
        /// <param name="value">The data to wrap.</param>
        public static implicit operator HubconResponse<T>(Exception value)
        {
            if(value is OperationCanceledException)
                return HubconResponse.Cancelled<T>(value, value.Message);
            else
                return HubconResponse.InternalError<T>(value);
        }

        /// <summary>
        /// Creates a non-generic <see cref="IHubconResponse"/> version of this response (boxing the data).
        /// </summary>
        /// <returns>An <see cref="IHubconResponse"/> instance.</returns>
        public IHubconResponse GetBoxed()
        {
            return new HubconResponse(Success, Failure, Message, Error, StatusCode, Data!, OriginalResponse!, Exception!);
        }
    }
}