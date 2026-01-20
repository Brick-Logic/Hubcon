using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface ITelemetryProvider
    {
        event Action? OnTelemetryUpdated;
        event Action<IRequestsPerSecondSnapshot>? OnRequestsPerSecondUpdated;

        // Process telemetry
        Func<double>? GetCurrentCPU { get; }
        Func<Process>? GetCurrentProcess { get; }
        Func<double>? GetCurrentHeapSize { get; }
        Func<int>? GetThreadCount { get; }

        // Websockets telemetry
        Func<int>? GetCurrentWebsocketClients { get; }
        Func<int>? CurrentSubscriptionCount { get; }
        Func<int>? CurrentIngestCount { get; }
        Func<int>? CurrentStreamingsCount { get; }
        Func<int>? CurrentWebSocketsRequestsCount { get; }
        Func<int>? CurrentWebSocketsCallRequestsCount { get; }
        Func<int>? CurrentWebSocketsRoundTripRequestsCount { get; }

        // HTTP telemetry
        Func<int>? ActiveHttpRequestsCount { get; }
        Func<int>? CurrentHttpRoundTripRequestsCount { get; }
        Func<int>? CurrentHttpCallRequestsCount { get; }
        Func<int>? CurrentRequestsPerSecond { get; }

        Func<int>? CurrentSubscriptionPerSecond { get; }
        Func<int>? CurrentStreamingsPerSecond { get; }
        Func<int>? CurrentIngestPerSecond { get; }
        Func<int>? CurrentWebSocketsRequestsPerSecond { get; }
        Func<int>? CurrentWebSocketsCallRequestsPerSecond { get; }
        Func<int>? CurrentWebSocketsRoundTripRequestsPerSecond { get; }


        Func<int>? CurrentHttpRequestsPerSecond { get; }
        Func<int>? CurrentHttpCallRequestsPerSecond { get; }
        Func<int>? CurrentHttpRoundTripRequestsPerSecond { get; }

        void RegisterProvider<T>(Expression<Func<ITelemetryProvider, T>> providerProperty, T provider);
        void CallOnRequestsPerSecondUpdated(IRequestsPerSecondSnapshot snapshot);
        void CallOnTelemetryUpdated();
    }
}
