using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a point-in-time snapshot of the request throughput across the system.
    /// Provides granular metrics categorized by transport protocol and Hubcon operation type.
    /// </summary>
    public interface IRequestsPerSecondSnapshot
    {
        /// <summary>Gets the current number of active subscription requests per second.</summary>
        int SubscriptionsPerSecond { get; }

        /// <summary>Gets the current number of data streaming requests per second.</summary>
        int StreamingsPerSecond { get; }

        /// <summary>Gets the current number of high-throughput ingestion requests per second.</summary>
        int IngestsPerSecond { get; }

        /// <summary>Gets the total aggregated number of requests per second across all types.</summary>
        int RequestsPerSecond { get; }

        /// <summary>Gets the total number of requests handled via the WebSocket transport per second.</summary>
        int WebSocketsRequestsPerSecond { get; }

        /// <summary>Gets the number of WebSocket requests requiring a round-trip response per second.</summary>
        int WebSocketsRoundTripRequestsPerSecond { get; }

        /// <summary>Gets the number of HTTP fire-and-forget (Call) requests per second.</summary>
        int HttpCallRequestsPerSecond { get; }

        /// <summary>Gets the number of HTTP requests requiring a round-trip response per second.</summary>
        int HttpRoundTripRequestsPerSecond { get; }

        /// <summary>Gets the number of WebSocket fire-and-forget (Call) requests per second.</summary>
        int WebSocketsCallRequestsPerSecond { get; }

        /// <summary>Gets the total number of requests handled via the HTTP transport per second.</summary>
        int HttpRequestsPerSecond { get; }
    }
}
