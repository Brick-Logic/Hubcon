using Hubcon.Server.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
#pragma warning disable CS1591

namespace Hubcon.Server.Core.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        public event Action<ITelemetryService>? OnTelemetryUpdated;
        public event Action<ITelemetryService, IRequestsPerSecondSnapshot>? OnRequestsPerSecondUpdated;

        private readonly ITelemetryProvider provider;
        public TelemetryService(ITelemetryProvider provider)
        {
            this.provider = provider;
            provider.OnTelemetryUpdated += Provider_OnTelemetryUpdated;
            provider.OnRequestsPerSecondUpdated += Provider_OnRequestsPerSecondUpdated;
        }

        private void Provider_OnTelemetryUpdated() => OnTelemetryUpdated?.Invoke(this);
        private void Provider_OnRequestsPerSecondUpdated(IRequestsPerSecondSnapshot snapshot) => OnRequestsPerSecondUpdated?.Invoke(this, snapshot);
        
        private static T GetValue<T>(Func<T>? provider, T defaultValue)
        {
            if (provider == null)
                return defaultValue;

            return provider.Invoke();
        }

        public int CurrentWebSocketClients => GetValue(provider.GetCurrentWebsocketClients, -1);
        public double CurrentCPU => GetValue(provider.GetCurrentWebsocketClients, -1);
        public Process CurrentProcess => GetValue(provider.GetCurrentProcess, default!);
        public double CurrentHeapSize => GetValue(provider.GetCurrentHeapSize, -1);
        public int CurrentThreads => GetValue(provider.GetThreadCount, -1);

        public int ActiveSubscriptionCount => GetValue(provider.CurrentSubscriptionCount, -1);
        public int ActiveIngestCount => GetValue(provider.CurrentIngestCount, -1);
        public int ActiveStreamingsCount => GetValue(provider.CurrentStreamingsCount, -1);
        public int ActiveWebSocketsRequestsCount => GetValue(provider.CurrentWebSocketsRequestsCount, -1);
        public int ActiveWebSocketsCallRequestsCount => GetValue(provider.CurrentWebSocketsCallRequestsCount, -1);
        public int ActiveWebSocketsRoundTripRequestsCount => GetValue(provider.CurrentWebSocketsRoundTripRequestsCount, -1);

        public int ActiveHttpRequestsCount => GetValue(provider.ActiveHttpRequestsCount, -1);
        public int ActiveHttpRoundTripRequestsCount => GetValue(provider.CurrentHttpRoundTripRequestsCount, -1);
        public int ActiveHttpCallRequestsCount => GetValue(provider.CurrentHttpCallRequestsCount, -1);

        ~TelemetryService()
        {
            provider.OnTelemetryUpdated -= Provider_OnTelemetryUpdated;
            provider.OnRequestsPerSecondUpdated -= Provider_OnRequestsPerSecondUpdated;
        }
    }
}
