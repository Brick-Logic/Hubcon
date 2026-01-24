using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Standard.Interfaces
{
    public interface IHubconResponse : IHubconResponse<object?>
    {
    }

    public interface IHubconResponse<T>
    {
        [JsonPropertyName("error")]
        string Error { get; }

        [JsonIgnore]
        Exception? Exception { get; }

        [JsonPropertyName("failure")]
        bool Failure { get; }

        [JsonPropertyName("message")]
        string Message { get; }

        [JsonPropertyName("statusCode")]
        int StatusCode { get; }

        [JsonPropertyName("success")]
        bool Success { get; }

        [JsonPropertyName("data")]
        T? Data { get; }
    }
}