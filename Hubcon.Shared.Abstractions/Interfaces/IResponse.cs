using System;
using System.Text.Json.Serialization;

namespace Hubcon
{
    /// <summary>
    /// Defines the base contract for all responses within the Hubcon framework.
    /// Provides status information, success/failure flags, and error metadata.
    /// </summary>
    public interface IResponse
    {
        /// <summary>
        /// Gets the protocol-specific status code (e.g., HTTP 200, 404, 500).
        /// </summary>
        [JsonPropertyName("statusCode")]
        int StatusCode { get; }

        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        [JsonPropertyName("success")]
        bool Success { get; }

        /// <summary>
        /// Gets a value indicating whether the operation resulted in a failure.
        /// </summary>
        [JsonPropertyName("failure")]
        bool Failure { get; }

        /// <summary>
        /// Gets the error message describing the failure, if applicable.
        /// </summary>
        [JsonPropertyName("error")]
        string? Error { get; }

        /// <summary>
        /// Gets or sets the <see cref="Exception"/> associated with the response, if any.
        /// <remarks>This property is ignored during JSON serialization.</remarks>
        /// </summary>
        [JsonIgnore]
        Exception? Exception { get; set; }

        /// <summary>
        /// Gets a descriptive message indicating the result of the operation.
        /// </summary>
        [JsonPropertyName("message")]
        string? Message { get; }

        /// <summary>
        /// Converts the current response into a boxed <see cref="IHubconResponse"/> version.
        /// </summary>
        /// <returns>An instance of <see cref="IHubconResponse"/>.</returns>
        IHubconResponse GetBoxed();
        
        /// <summary>
        /// Converts the current response into a boxed <see cref="object"/>.
        /// </summary>
        object? GetOriginal();
    }

    /// <summary>
    /// Represents a non-generic Hubcon response that wraps an <see cref="object"/> payload.
    /// </summary>
    public interface IHubconResponse : IHubconResponse<object>
    {
    }

    /// <summary>
    /// Defines a strongly-typed response containing a data payload of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data contained in the response.</typeparam>
    public interface IHubconResponse<out T> : IResponse
    {
        /// <summary>
        /// Gets the data payload returned by the operation.
        /// </summary>
        [JsonPropertyName("data")]
        T? Data { get; }
    }
}