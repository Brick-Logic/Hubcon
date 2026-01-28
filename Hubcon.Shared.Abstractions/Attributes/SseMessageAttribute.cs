using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Shared.Abstractions.Attributes
{
    public abstract class SseMessageAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ParseSseMessageAttribute : SseMessageAttribute
    {
        public ParseSseMessageAttribute(string messageName, string jsonPropertyName = "")
        {
            MessageName = messageName;
            JsonPropertyName = jsonPropertyName;
        }

        public string MessageName { get; }
        public string JsonPropertyName { get; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ParseEndSseMessageAttribute : SseMessageAttribute
    {
        public ParseEndSseMessageAttribute(string messageName)
        {
            MessageName = messageName;
        }

        public string MessageName { get; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ParseRawSseMessageAttribute : SseMessageAttribute
    {
    }
}
