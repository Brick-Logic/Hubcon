using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface ITelemetryService
    {
        int CurrentWebSocketClients { get; }
        double CurrentCPU { get; }
        Process CurrentProcess { get; }
        double CurrentHeapSize { get; }
        int CurrentThreads { get; }
        int ActiveSubscriptionCount { get; }
        int ActiveIngestCount { get; }
        int ActiveStreamingsCount { get; }
        int ActiveWebSocketsCallRequestsCount { get; }
        int ActiveHttpRequestsCount { get; }
        int ActiveHttpRoundTripRequestsCount { get; }

        event Action<ITelemetryService>? OnTelemetryUpdated;
        event Action<ITelemetryService, IRequestsPerSecondSnapshot>? OnRequestsPerSecondUpdated;
    }
}
