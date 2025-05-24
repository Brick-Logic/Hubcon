using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Models
{
    public class HubconGraphQLRequest
    {
        public string? Query { get; set; }
        public Dictionary<string, JsonElement> Variables { get; } = new();
    }
}