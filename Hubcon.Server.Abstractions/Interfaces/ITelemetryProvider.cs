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
        /// <summary>Gets the number of currently connected WebSocket clients.</summary>
        Func<int>? GetCurrentWebsocketClients { get; }
        
        /// <summary>Gets a function that returns the current CPU usage percentage.</summary>
        Func<double>? GetCurrentCPU { get; }

        /// <summary>Gets a function that returns the current system <see cref="Process"/> information.</summary>
        Func<Process>? GetCurrentProcess { get; }

        /// <summary>Gets a function that returns the current Managed Heap size in MB.</summary>
        Func<double>? GetCurrentHeapSize { get; }

        /// <summary>Gets a function that returns the current thread count of the process.</summary>
        Func<int>? GetThreadCount { get; }
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
