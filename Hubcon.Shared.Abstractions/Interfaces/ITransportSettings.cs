using System;
using System.Threading.RateLimiting;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Hubcon
{
    /// <summary>
    /// Defines the core configuration settings required to control the behavior, security limits, 
    /// timeouts, and operational features of a transport layer within the Hubcon framework.
    /// </summary>
    public interface ITransportSettings
    {
        /// <summary>
        /// The maximum allowable message size in bytes for incoming payloads.
        /// </summary>
        /// <value>
        /// The maximum message size in bytes. Payloads exceeding this limit will be rejected to prevent memory exhaustion attacks.
        /// </value>
        public long MaxMessageSizeInBytes { get; }

        /// <summary>
        /// The default timeout duration for general operation requests.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> representing the request timeout.
        /// </value>
        public TimeSpan RequestTimeout { get; }

        /// <summary>
        /// The maximum total number of concurrent active connections allowed on the transport.
        /// </summary>
        /// <value>
        /// An integer representing the global connection limit across all remote clients.
        /// </value>
        public int MaxConnections { get; }

        /// <summary>
        /// The maximum number of concurrent active connections allowed from a single remote IP address.
        /// </summary>
        /// <value>
        /// The maximum connection count per IP address. Used as a transport-level defense against DoS attacks.
        /// </value>
        public int MaxConnectionsPerIp { get; }

        /// <summary>
        /// The value indicating whether heartbeats (Ping frames) are enabled for connection liveness checks.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if Ping keep-alive is enabled; otherwise, <see langword="false"/>.
        /// </value>
        public bool EnablePing { get; }

        /// <summary>
        /// The rate limiting options applied specifically to incoming Ping frames.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> for ping operations, or <see langword="null"/> if unthrottled.
        /// </value>
        public int PingOperationLimitPerSecond { get; }

        /// <summary>
        /// Gets a value indicating whether the transport automatically responds to Ping frames with Pong frames.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if automatic Pong responses are enabled; otherwise, <see langword="false"/>.
        /// </value>
        public bool EnablePong { get; }

        /// <summary>
        /// The URL path or protocol route prefix used by this transport instance.
        /// </summary>
        /// <value>
        /// A string representing the endpoint prefix (e.g., <c>"/hubcon/v1"</c>).
        /// </value>
        public string TransportPrefix { get; }

        /// <summary>
        /// Gets a value indicating whether transient message retries are supported by the client/transport pipeline.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if retry mechanics are enabled; otherwise, <see langword="false"/>.
        /// </value>
        public bool RetryableMessagesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether rate limiters are globally enforced across operations and endpoints.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to enable rate limiting checks; <see langword="false"/> to bypass rate limiting completely.
        /// </value>
        public bool UseRateLimiters { get; }

        /// <summary>
        /// Gets a value indicating whether transport-level execution logging and diagnostic traces are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if logging is active; otherwise, <see langword="false"/>.
        /// </value>
        public bool LoggingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether a client is allowed to remotely request cancellation of an in-flight operation.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to link client-sent cancellation tokens to server operations; otherwise, <see langword="false"/>.
        /// </value>
        public bool AllowRemoteCancellation { get; }

        /// <summary>
        /// The global transport-level rate limiting bucket options.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> applied to all incoming transport traffic.
        /// </value>
        public int TransportLimitPerSecond { get; }

        /// <summary>
        /// Gets a value indicating whether RPC method overloading (multiple endpoints with the same name but different parameters) is supported.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to allow method overloading during dispatch routing; otherwise, <see langword="false"/>.
        /// </value>
        public bool MethodOverloadingEnabled { get; }

        /// <summary>
        /// The maximum number of concurrent in-flight requests permitted from a single IP address.
        /// </summary>
        /// <value>
        /// The concurrency threshold per remote IP.
        /// </value>
        public int MaxConcurrentRequestsPerIp { get; }

        /// <summary>
        /// Gets a value indicating whether unauthenticated or anonymous clients are permitted to establish connections.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if anonymous access is permitted; <see langword="false"/> if authentication tokens are mandatory.
        /// </value>
        public bool AllowAnonymousClients { get; }

        /// <summary>
        /// The security parameters used to validate authentication tokens (e.g., JWT) supplied during transport handshake.
        /// </summary>
        /// <value>
        /// The <see cref="TokenValidationParameters"/> used for identity verification, or <see langword="null"/> if authentication is disabled.
        /// </value>
        public TokenValidationParameters? TokenValidationParameters { get; }
        
        /// <summary>
        /// Gets a value indicating whether non-blocking one-way (Call) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if Call operations are accepted; otherwise, <see langword="false"/>.
        /// </value>
        public bool CallOperationEnabled { get; }

        /// <summary>
        /// The timeout applied to processing a one-way (Call) operation pipeline.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the maximum allowed execution time.
        /// </value>
        public TimeSpan CallOperationTimeout { get; }

        /// <summary>
        /// The rate limiting configuration for one-way (Call) operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> governing Call invocation rates.
        /// </value>
        public int CallOperationLimitPerSecond { get; }
        
        /// <summary>
        /// Gets a value indicating whether synchronous or asynchronous Request-Response (Invoke) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if Invoke operations are accepted; otherwise, <see langword="false"/>.
        /// </value>
        public bool InvokeOperationEnabled { get; }

        /// <summary>
        /// The timeout duration for completing a Request-Response (Invoke) operation before returning a timeout error to the client.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the maximum duration allowed for invocation response.
        /// </value>
        public TimeSpan InvokeOperationTimeout { get; }

        /// <summary>
        /// The rate limiting configuration applied specifically to Request-Response (Invoke) operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> governing Invoke invocation rates.
        /// </value>
        public int InvokeOperationLimitPerSecond { get; }
        
        /// <summary>
        /// Gets a value indicating whether Server-Streaming (Stream) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if server streaming is allowed; otherwise, <see langword="false"/>.
        /// </value>
        public bool StreamOperationEnabled { get; }

        /// <summary>
        /// The maximum lifetime or inactivity timeout permitted for an active stream operation.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> representing the streaming timeout limit.
        /// </value>
        public TimeSpan StreamOperationTimeout { get; }

        /// <summary>
        /// The rate limiting options governing the creation rate of Server-Streaming operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> for stream initialization.
        /// </value>
        public int StreamOperationLimitPerSecond { get; }
        
        /// <summary>
        /// Gets a value indicating whether Client-Streaming (Ingest) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if client data ingestion is permitted; otherwise, <see langword="false"/>.
        /// </value>
        public bool IngestOperationEnabled { get; }

        /// <summary>
        /// The maximum processing time allowed for a single client ingestion stream before closing the pipeline.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the ingestion timeout.
        /// </value>
        public TimeSpan IngestOperationTimeout { get; }

        /// <summary>
        /// The rate limiting configuration for initiating Client-Streaming (Ingest) operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> for stream ingestion limits.
        /// </value>
        public int IngestOperationLimitPerSecond { get; }
        
        /// <summary>
        /// The rate limiting configuration for transport-only control messages.
        /// </summary>
        /// <value>
        /// The <see cref="ControlMessagesPerSecond"/> for control messages rate limits.
        /// </value>
        public int ControlMessagesPerSecond { get; }
        
        /// <summary>
        /// Gets a value indicating whether the transport should check the token used for the live connection on every received message.
        /// </summary>
        public bool CheckTokenExpirationOnMessageReceived { get; }
        
        /// <summary>
        /// The <see cref="Type"/> value for the assigned authentication handler corresponding to this transport.
        /// </summary>
        public Type? ConnectionAuthHandlerType { get; }
        
        /// <summary>
        /// The maximum time allowed for a connection to be alive.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the connection timeout.
        /// </value>
        public TimeSpan ConnectionTimeout { get; }

        /// <summary>
        /// Gets a value that determines if the transport layer requires transport-level auth.
        /// </summary>
        bool RequiresAuth { get; }
        
        /// <summary>
        /// Determines which authority will handle the rate limiting. By default, a local <see cref="IRateLimitAuthority"/> will be used. 
        /// </summary>
        public IRateLimitAuthority? TransportRateLimitAuthority { get; }
    }
    
    /// <summary>
    /// Defines the core configuration settings required to control the behavior, security limits, 
    /// timeouts, and operational features of a transport layer within the Hubcon framework.
    /// </summary>
    public interface ISettableTransportSettings : ITransportSettings
    {
        /// <summary>
        /// The maximum allowable message size in bytes for incoming payloads.
        /// </summary>
        /// <value>
        /// The maximum message size in bytes. Payloads exceeding this limit will be rejected to prevent memory exhaustion attacks.
        /// </value>
        public new long MaxMessageSizeInBytes { get; set; }

        /// <summary>
        /// The default timeout duration for general operation requests.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> representing the request timeout.
        /// </value>
        public new TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// The maximum total number of concurrent active connections allowed on the transport.
        /// </summary>
        /// <value>
        /// An integer representing the global connection limit across all remote clients.
        /// </value>
        public new int MaxConnections { get; set; }

        /// <summary>
        /// The maximum number of concurrent active connections allowed from a single remote IP address.
        /// </summary>
        /// <value>
        /// The maximum connection count per IP address. Used as a transport-level defense against DoS attacks.
        /// </value>
        public new int MaxConnectionsPerIp { get; set; }

        /// <summary>
        /// The value indicating whether heartbeats (Ping frames) are enabled for connection liveness checks.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if Ping keep-alive is enabled; otherwise, <see langword="false"/>.
        /// </value>
        public new bool EnablePing { get; set; }

        /// <summary>
        /// The rate limiting options applied specifically to incoming Ping frames.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> for ping operations, or <see langword="null"/> if unthrottled.
        /// </value>
        public new int PingOperationLimitPerSecond { get; set; }

        /// <summary>
        /// Gets a value indicating whether the transport automatically responds to Ping frames with Pong frames.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if automatic Pong responses are enabled; otherwise, <see langword="false"/>.
        /// </value>
        public new bool EnablePong { get; set; }

        /// <summary>
        /// The URL path or protocol route prefix used by this transport instance.
        /// </summary>
        /// <value>
        /// A string representing the endpoint prefix (e.g., <c>"/hubcon/v1"</c>).
        /// </value>
        public new string TransportPrefix { get; set; }

        /// <summary>
        /// Gets a value indicating whether transient message retries are supported by the client/transport pipeline.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if retry mechanics are enabled; otherwise, <see langword="false"/>.
        /// </value>
        public new bool RetryableMessagesEnabled { get; set; }

        /// <summary>
        /// Gets a value indicating whether rate limiters are globally enforced across operations and endpoints.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to enable rate limiting checks; <see langword="false"/> to bypass rate limiting completely.
        /// </value>
        public new bool UseRateLimiters { get; set; }

        /// <summary>
        /// Gets a value indicating whether transport-level execution logging and diagnostic traces are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if logging is active; otherwise, <see langword="false"/>.
        /// </value>
        public new bool LoggingEnabled { get; set; }

        /// <summary>
        /// Gets a value indicating whether a client is allowed to remotely request cancellation of an in-flight operation.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to link client-sent cancellation tokens to server operations; otherwise, <see langword="false"/>.
        /// </value>
        public new bool AllowRemoteCancellation { get; set; }

        /// <summary>
        /// The global transport-level rate limiting bucket options.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> applied to all incoming transport traffic.
        /// </value>
        public new int TransportLimitPerSecond { get; set; }

        /// <summary>
        /// Gets a value indicating whether RPC method overloading (multiple endpoints with the same name but different parameters) is supported.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to allow method overloading during dispatch routing; otherwise, <see langword="false"/>.
        /// </value>
        public new bool MethodOverloadingEnabled { get; set; }

        /// <summary>
        /// The maximum number of concurrent in-flight requests permitted from a single IP address.
        /// </summary>
        /// <value>
        /// The concurrency threshold per remote IP.
        /// </value>
        public new int MaxConcurrentRequestsPerIp { get; set; }

        /// <summary>
        /// Gets a value indicating whether unauthenticated or anonymous clients are permitted to establish connections.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if anonymous access is permitted; <see langword="false"/> if authentication tokens are mandatory.
        /// </value>
        public new bool AllowAnonymousClients { get; set; }

        /// <summary>
        /// The security parameters used to validate authentication tokens (e.g., JWT) supplied during transport handshake.
        /// </summary>
        /// <value>
        /// The <see cref="TokenValidationParameters"/> used for identity verification, or <see langword="null"/> if authentication is disabled.
        /// </value>
        public new TokenValidationParameters? TokenValidationParameters { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether non-blocking one-way (Call) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if Call operations are accepted; otherwise, <see langword="false"/>.
        /// </value>
        public new bool CallOperationEnabled { get; set; }

        /// <summary>
        /// The timeout applied to processing a one-way (Call) operation pipeline.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the maximum allowed execution time.
        /// </value>
        public new TimeSpan CallOperationTimeout { get; set; }

        /// <summary>
        /// The rate limiting configuration for one-way (Call) operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> governing Call invocation rates.
        /// </value>
        public new int CallOperationLimitPerSecond { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether synchronous or asynchronous Request-Response (Invoke) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if Invoke operations are accepted; otherwise, <see langword="false"/>.
        /// </value>
        public new bool InvokeOperationEnabled { get; set; }

        /// <summary>
        /// The timeout duration for completing a Request-Response (Invoke) operation before returning a timeout error to the client.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the maximum duration allowed for invocation response.
        /// </value>
        public new TimeSpan InvokeOperationTimeout { get; set; }

        /// <summary>
        /// The rate limiting configuration applied specifically to Request-Response (Invoke) operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> governing Invoke invocation rates.
        /// </value>
        public new int InvokeOperationLimitPerSecond { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether Server-Streaming (Stream) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if server streaming is allowed; otherwise, <see langword="false"/>.
        /// </value>
        public new bool StreamOperationEnabled { get; set; }

        /// <summary>
        /// The maximum lifetime or inactivity timeout permitted for an active stream operation.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> representing the streaming timeout limit.
        /// </value>
        public new TimeSpan StreamOperationTimeout { get; set; }

        /// <summary>
        /// The rate limiting options governing the creation rate of Server-Streaming operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> for stream initialization.
        /// </value>
        public new int StreamOperationLimitPerSecond { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether Client-Streaming (Ingest) operations are enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if client data ingestion is permitted; otherwise, <see langword="false"/>.
        /// </value>
        public new bool IngestOperationEnabled { get; set; }

        /// <summary>
        /// The maximum processing time allowed for a single client ingestion stream before closing the pipeline.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the ingestion timeout.
        /// </value>
        public new TimeSpan IngestOperationTimeout { get; set; }

        /// <summary>
        /// The rate limiting configuration for initiating Client-Streaming (Ingest) operations.
        /// </summary>
        /// <value>
        /// The <see cref="int"/> for stream ingestion limits.
        /// </value>
        public new int IngestOperationLimitPerSecond { get; set; }
        
        /// <summary>
        /// The rate limiting configuration for transport-only control messages.
        /// </summary>
        /// <value>
        /// The <see cref="ControlMessagesLimitPerSecond"/> for control messages rate limits.
        /// </value>
        public new int ControlMessagesLimitPerSecond { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether the transport should check the token used for the live connection on every received message.
        /// </summary>
        public new bool CheckTokenExpirationOnMessageReceived { get; set; }
        
        /// <summary>
        /// The <see cref="Type"/> value for the assigned authentication handler corresponding to this transport.
        /// </summary>
        public new Type? ConnectionAuthHandlerType { get; set; }
        
        /// <summary>
        /// Defines which auth handler should be used for transport-level authentication.
        /// </summary>
        /// <typeparam name="T">A Hubcon Auth Handler.</typeparam>
        public void UseAuthHandler<T>() where T : class, IHubconAuthHandler;
        
        /// <summary>
        /// The security parameters used to validate authentication tokens (e.g., JWT) supplied during transport handshake in compatible transports.
        /// </summary>
        /// <param name="tokenValidationParameters">The token validation parameters.</param>
        public void UseTokenValidationParameters(TokenValidationParameters tokenValidationParameters);
        
        /// <summary>
        /// The maximum time allowed for a connection to be alive.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> specifying the connection timeout.
        /// </value>
        public new TimeSpan ConnectionTimeout { get; set; }

        /// <summary>
        /// Gets a value that determines if the transport layer requires transport-level auth.
        /// </summary>
        public new bool RequiresAuth { get; set; }
        
        /// <summary>
        /// Determines which authority will handle the rate limiting. <see cref="IRateLimitAuthority"/>
        /// </summary>
        public new IRateLimitAuthority? TransportRateLimitAuthority { get; set; }
    }
}