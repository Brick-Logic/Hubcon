using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a centralized provider for framework and system telemetry.
    /// Orchestrates the collection of process-level metrics, real-time throughput, 
    /// and protocol-specific connection counts.
    /// </summary>
    public interface ITelemetryProvider
    {
        /// <summary>Occurs when general telemetry data (CPU, Memory, etc.) has been refreshed.</summary>
        event Action? OnTelemetryUpdated;

        /// <summary>Occurs when throughput metrics (Requests Per Second) have been updated.</summary>
        event Action<IRequestsPerSecondSnapshot>? OnRequestsPerSecondUpdated;

        #region Process & System Telemetry
        /// <summary>Gets a function that returns the current CPU usage percentage.</summary>
        Func<double>? GetCurrentCPU { get; }

        /// <summary>Gets a function that returns the current system <see cref="Process"/> information.</summary>
        Func<Process>? GetCurrentProcess { get; }

        /// <summary>Gets a function that returns the current Managed Heap size in MB.</summary>
        Func<double>? GetCurrentHeapSize { get; }

        /// <summary>Gets a function that returns the current thread count of the process.</summary>
        Func<int>? GetThreadCount { get; }
        #endregion

        #region WebSocket Telemetry (Stateful)
        /// <summary>Gets the number of currently connected WebSocket clients.</summary>
        Func<int>? GetCurrentWebsocketClients { get; }

        /// <summary>Gets the total number of active subscriptions (Events/Topic-based).</summary>
        Func<int>? CurrentSubscriptionCount { get; }

        /// <summary>Gets the total number of active high-throughput ingestion flows.</summary>
        Func<int>? CurrentIngestCount { get; }

        /// <summary>Gets the total number of active data streams.</summary>
        Func<int>? CurrentStreamingsCount { get; }

        /// <summary>Gets the total count of requests processed via WebSockets.</summary>
        Func<int>? CurrentWebSocketsRequestsCount { get; }

        /// <summary>Gets the count of Fire-and-Forget (Call) requests processed via WebSockets.</summary>
        Func<int>? CurrentWebSocketsCallRequestsCount { get; }

        /// <summary>Gets the count of Round-Trip requests processed via WebSockets.</summary>
        Func<int>? CurrentWebSocketsRoundTripRequestsCount { get; }
        #endregion

        #region HTTP Telemetry (Stateless)
        /// <summary>Gets the number of HTTP requests currently being processed.</summary>
        Func<int>? ActiveHttpRequestsCount { get; }

        /// <summary>Gets the count of Round-Trip requests processed via HTTP.</summary>
        Func<int>? CurrentHttpRoundTripRequestsCount { get; }

        /// <summary>Gets the count of Fire-and-Forget (Call) requests processed via HTTP.</summary>
        Func<int>? CurrentHttpCallRequestsCount { get; }
        #endregion

        #region Throughput Metrics (Requests Per Second)
        /// <summary>Gets the global aggregated requests per second.</summary>
        Func<int>? CurrentRequestsPerSecond { get; }

        /// <summary>Gets the subscriptions initiated per second.</summary>
        Func<int>? CurrentSubscriptionPerSecond { get; }

        /// <summary>Gets the stream frames processed per second.</summary>
        Func<int>? CurrentStreamingsPerSecond { get; }

        /// <summary>Gets the ingestion packets processed per second.</summary>
        Func<int>? CurrentIngestPerSecond { get; }

        /// <summary>Gets the total WebSocket requests per second.</summary>
        Func<int>? CurrentWebSocketsRequestsPerSecond { get; }

        /// <summary>Gets the WebSocket fire-and-forget calls per second.</summary>
        Func<int>? CurrentWebSocketsCallRequestsPerSecond { get; }

        /// <summary>Gets the WebSocket round-trip calls per second.</summary>
        Func<int>? CurrentWebSocketsRoundTripRequestsPerSecond { get; }

        /// <summary>Gets the total HTTP requests per second.</summary>
        Func<int>? CurrentHttpRequestsPerSecond { get; }

        /// <summary>Gets the HTTP fire-and-forget calls per second.</summary>
        Func<int>? CurrentHttpCallRequestsPerSecond { get; }

        /// <summary>Gets the HTTP round-trip calls per second.</summary>
        Func<int>? CurrentHttpRoundTripRequestsPerSecond { get; }
        #endregion

        /// <summary>
        /// Registers a specific telemetry provider delegate for a given property.
        /// </summary>
        /// <typeparam name="T">The delegate type (e.g., <c>Func&lt;int&gt;</c>).</typeparam>
        /// <param name="providerProperty">An expression selecting the property to register.</param>
        /// <param name="provider">The delegate that provides the live data.</param>
        void RegisterProvider<T>(Expression<Func<ITelemetryProvider, T>> providerProperty, T provider);

        /// <summary>
        /// Manually triggers the <see cref="OnRequestsPerSecondUpdated"/> event with a new snapshot.
        /// </summary>
        void CallOnRequestsPerSecondUpdated(IRequestsPerSecondSnapshot snapshot);

        /// <summary>
        /// Manually triggers the <see cref="OnTelemetryUpdated"/> event.
        /// </summary>
        void CallOnTelemetryUpdated();
    }
}
