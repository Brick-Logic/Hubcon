using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Shared.Abstractions.Attributes
{
    /// <summary>
    /// Represents a base attribute for Server-Sent Events (SSE) message handling.
    /// This class serves as the foundation for specific SSE message attribute types.
    /// </summary>
    public abstract class SseMessageAttribute : Attribute
    {
    }

    /// <summary>
    /// Indicates a methodthat is responsible for parsing a specific SSE message.
    /// Multiple instances of this attribute can be applied to a single method to handle various messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ParseSseMessageAttribute : SseMessageAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParseSseMessageAttribute"/> class.
        /// </summary>
        /// <param name="messageName">The name of the SSE message to parse.</param>
        /// <param name="jsonPropertyName">Optional JSON property name associated with the message.</param>
        public ParseSseMessageAttribute(string messageName, string jsonPropertyName = "")
        {
            MessageName = messageName;
            JsonPropertyName = jsonPropertyName;
        }

        /// <summary>
        /// Gets the name of the SSE message this attribute handles.
        /// </summary>
        public string MessageName { get; }

        /// <summary>
        /// Gets the JSON property name associated with this message, if specified.
        /// </summary>
        public string JsonPropertyName { get; }
    }

    /// <summary>
    /// Indicates a method responsible for handling the end of a specific SSE message.
    /// Only one such attribute can be applied to a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ParseEndSseMessageAttribute : SseMessageAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParseEndSseMessageAttribute"/> class.
        /// </summary>
        /// <param name="messageName">The name of the SSE message signaling its end.</param>
        public ParseEndSseMessageAttribute(string messageName)
        {
            MessageName = messageName;
        }

        /// <summary>
        /// Gets the name of the SSE message for which this attribute indicates the end.
        /// </summary>
        public string MessageName { get; }
    }

    /// <summary>
    /// Marks a method that will handle raw SSE messages.
    /// Only one such attribute can be applied to a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ParseRawSseMessageAttribute : SseMessageAttribute
    {
    }
}
