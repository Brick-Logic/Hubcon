namespace Hubcon.Shared.Abstractions.Enums
{
    /// <summary>
    /// Represents the types of hooks that can be triggered during an operation.
    /// </summary>
    /// <remarks>Use these values to specify the type of hook to handle in your implementation.</remarks>
    public enum HookType
    {
        /// <summary>
        /// Triggered before sending the operation. Applies to all operations.
        /// </summary>
        OnSend,

        /// <summary>
        /// Triggered immediately after the operation is sent. Applies to all operations.
        /// </summary>
        OnAfterSend,

        /// <summary>
        /// Triggered when a response is received for the operation. Applies to all operations.
        /// </summary>
        OnResponse,

        /// <summary>
        /// Triggered when an error occurs during the operation. Applies to all operations.
        /// </summary>
        OnError,

        /// <summary>
        /// Triggered when a subscription is successfully established. Subscriptions and streams only.
        /// </summary>
        OnSubscribed,

        /// <summary>
        /// Triggered when a subscription is successfully terminated. Subscriptions and streams only.
        /// </summary>
        OnUnsubscribed,

        /// <summary>
        /// Triggered when an event related to the operation is received. Subscriptions and streams only.
        /// </summary>
        OnEventReceived,

        /// <summary>
        /// Triggered when the ingestion process for the operation is completed. Ingest operations only.
        /// </summary>
        OnIngestCompleted
    }
}