using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Defines the high-level service for consuming framework and system telemetry.
    /// Provides a unified, property-based view of the current resource usage, 
    /// active connection counts, and real-time throughput metrics.
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>Gets the current number of active WebSocket client connections.</summary>
        int CurrentWebSocketClients { get; }

        /// <summary>Gets the current CPU usage percentage of the host system.</summary>
        double CurrentCPU { get; }

        /// <summary>Gets the system <see cref="Process"/> information for the current application instance.</summary>
        Process CurrentProcess { get; }

        /// <summary>Gets the current size of the managed heap in Megabytes (MB).</summary>
        double CurrentHeapSize { get; }

        /// <summary>Gets the current number of threads managed by the process.</summary>
        int CurrentThreads { get; }

        /// <summary>Gets the total number of active subscriptions (Events/Topics) across all clients.</summary>
        int ActiveSubscriptionCount { get; }

        /// <summary>Gets the total number of active high-throughput ingestion flows.</summary>
        int ActiveIngestCount { get; }

        /// <summary>Gets the total number of active outgoing data streams.</summary>
        int ActiveStreamingsCount { get; }

        /// <summary>Gets the total count of Fire-and-Forget (Call) requests currently being processed via WebSockets.</summary>
        int ActiveWebSocketsCallRequestsCount { get; }

        /// <summary>Gets the total number of HTTP requests currently in flight.</summary>
        int ActiveHttpRequestsCount { get; }

        /// <summary>Gets the total count of Round-Trip requests currently being processed via HTTP.</summary>
        int ActiveHttpRoundTripRequestsCount { get; }

        /// <summary>
        /// Occurs when general system telemetry (CPU, Memory, Threads) is refreshed.
        /// </summary>
        event Action<ITelemetryService>? OnTelemetryUpdated;

        /// <summary>
        /// Occurs when throughput metrics (Requests Per Second) are updated, providing 
        /// a detailed snapshot of the current traffic volume.
        /// </summary>
        event Action<ITelemetryService, IRequestsPerSecondSnapshot>? OnRequestsPerSecondUpdated;
    }
}
