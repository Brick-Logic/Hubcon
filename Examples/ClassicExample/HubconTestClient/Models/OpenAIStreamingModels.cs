using Hubcon;
using System.Text.Json.Serialization;

namespace HubconTestClient.Models
{
    public enum OpenAIEventType
    {
        [JsonPropertyName("response.created")] ResponseCreated,
        [JsonPropertyName("response.in_progress")] ResponseInProgress,
        [JsonPropertyName("response.output_item.added")] OutputItemAdded,
        [JsonPropertyName("response.content_part.added")] ContentPartAdded,
        [JsonPropertyName("response.output_text.delta")] OutputTextDelta,
        [JsonPropertyName("response.output_text.done")] OutputTextDone,
        [JsonPropertyName("response.content_part.done")] ContentPartDone,
        [JsonPropertyName("response.output_item.done")] OutputItemDone,
        [JsonPropertyName("response.completed")] ResponseCompleted,
        Unknown
    }

    // El evento raíz que recibís en cada línea de 'data: '
    public class OpenAIStreamEvent
    {
        [JsonPropertyName("type")]
        public string TypeRaw { get; set; } = string.Empty;

        // CAMBIO: De string? a int?
        [JsonPropertyName("item_id")] public string? ItemId { get; set; }
        [JsonPropertyName("output_index")] public long? OutputIndex { get; set; }
        [JsonPropertyName("content_index")] public long? ContentIndex { get; set; }

        [JsonPropertyName("delta")] public string? Delta { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }

        [JsonPropertyName("response")] public OpenAIResponse? Response { get; set; }

        [JsonPropertyName("event")] public EventType Event { get; set; }
    }

    public enum EventType
    {
        [JsonPropertyName("response.created")]
        ResponseCreated,

        [JsonDefault]
        Random
    }

    public class OpenAIStreamingResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("usage")] public OpenAIUsage? Usage { get; set; }
    }

    public class OpenAIUsage
    {
        [JsonPropertyName("total_tokens")] public string? TotalTokens { get; set; }
        [JsonPropertyName("input_tokens")] public string? InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public string? OutputTokens { get; set; }
    }


    public class OpenAIStreamRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "gpt-4.1";

        [JsonPropertyName("instructions")]
        public string Instructions { get; set; } = "You are a helpful assistant.";

        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = true;
    }
}
