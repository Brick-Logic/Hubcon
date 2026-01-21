namespace HubconTestClient.Models
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    public class CreateResponseCommand
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("input")]
        public string Input { get; set; }
    }

    public class OpenAIResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("background")]
        public bool Background { get; set; }

        [JsonPropertyName("billing")]
        public BillingInfo Billing { get; set; }

        [JsonPropertyName("completed_at")]
        public long CompletedAt { get; set; }

        [JsonPropertyName("error")]
        public object Error { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("output")]
        public List<OutputItem> Output { get; set; }

        [JsonPropertyName("usage")]
        public UsageInfo Usage { get; set; }

        [JsonPropertyName("reasoning")]
        public ReasoningInfo Reasoning { get; set; }

        [JsonPropertyName("text")]
        public TextConfig Text { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class BillingInfo
    {
        [JsonPropertyName("payer")]
        public string Payer { get; set; }
    }

    public class OutputItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public List<ContentItem> Content { get; set; }
    }

    public class ContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class UsageInfo
    {
        [JsonPropertyName("input_tokens")]
        public long InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public long OutputTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public long TotalTokens { get; set; }

        [JsonPropertyName("input_tokens_details")]
        public Dictionary<string, long> InputTokensDetails { get; set; }

        [JsonPropertyName("output_tokens_details")]
        public Dictionary<string, long> OutputTokensDetails { get; set; }
    }

    public class ReasoningInfo
    {
        [JsonPropertyName("effort")]
        public string Effort { get; set; }
    }

    public class TextConfig
    {
        [JsonPropertyName("verbosity")]
        public string Verbosity { get; set; }
    }

    public class OpenAIDeleteResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("deleted")]
        public bool Deleted { get; set; }
    }

    public class OpenAIList<T>
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();

        [JsonPropertyName("first_id")]
        public string? FirstId { get; set; }

        [JsonPropertyName("last_id")]
        public string? LastId { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    public class OpenAIMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } // "message"

        [JsonPropertyName("role")]
        public string Role { get; set; } // "user", "assistant"

        [JsonPropertyName("content")]
        public List<MessageContent> Content { get; set; } = new();
    }

    public class MessageContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } // "input_text", "output_text"

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
