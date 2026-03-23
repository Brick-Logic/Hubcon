namespace Hubcon
{
    /// <summary>
    /// Specifies the nature of an operation performed over the Hubcon transport.
    /// This determines how the request is handled and the expected behavior of the execution pipeline.
    /// </summary>
    public enum OperationKind
    {
        /// <summary>
        /// Represents a fire-and-forget operation where the client does not wait for a response from the server.
        /// Typically used for void or Task-returning methods where execution tracking is not required.
        /// </summary>
        CallMethod,

        /// <summary>
        /// Represents a standard remote procedure call (RPC) that expects a single response from the server.
        /// Typically used for Task-returning methods (Round-Trip).
        /// </summary>
        InvokeMethod,

        /// <summary>
        /// Represents an operation that establishes a continuous data stream between the client and the server.
        /// </summary>
        Stream,

        /// <summary>
        /// Represents a high-throughput data ingestion operation, optimized for pushing data to the server 
        /// with minimal overhead.
        /// </summary>
        Ingest
    }
}
