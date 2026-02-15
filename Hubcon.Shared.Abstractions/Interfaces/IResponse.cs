using System;
using System.Text.Json.Serialization;

namespace Hubcon
{
    public interface IResponse
    {
        [JsonPropertyName("statusCode")]
        int StatusCode { get; }

        [JsonPropertyName("success")]
        bool Success { get; }

        [JsonPropertyName("failure")]
        bool Failure { get; }

        [JsonPropertyName("error")]
        string Error { get; }

        [JsonIgnore]
        Exception Exception { get; set; }

        [JsonPropertyName("message")]
        string Message { get; }

        IHubconResponse GetBoxed();
    }

    public interface IHubconResponse : IHubconResponse<object>
    {
    }

    public interface IHubconResponse<T> : IResponse
    {
        [JsonPropertyName("data")]
        T Data { get; }
    }
}