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

        private readonly ITelemetryProvider _provider;
        public TelemetryService(ITelemetryProvider provider)
        {
            this._provider = provider;
            provider.OnTelemetryUpdated += Provider_OnTelemetryUpdated;
            provider.OnRequestsPerSecondUpdated += Provider_OnRequestsPerSecondUpdated;
        }

        private void Provider_OnTelemetryUpdated() => OnTelemetryUpdated?.Invoke(this);
        private void Provider_OnRequestsPerSecondUpdated(IRequestsPerSecondSnapshot snapshot) => OnRequestsPerSecondUpdated?.Invoke(this, snapshot);
        
        private static T GetValue<T>(Func<T>? provider, T defaultValue)
        {
            return provider == null ? defaultValue : provider.Invoke();
        }

        public int CurrentWebSocketClients => GetValue(_provider.GetCurrentWebsocketClients, -1);
        public double CurrentCPU => GetValue(_provider.GetCurrentCPU, -1);
        public Process CurrentProcess => GetValue(_provider.GetCurrentProcess, null!);
        public double CurrentHeapSize => GetValue(_provider.GetCurrentHeapSize, -1);
        public int CurrentThreads => GetValue(_provider.GetThreadCount, -1);
        
        ~TelemetryService()
        {
            _provider.OnTelemetryUpdated -= Provider_OnTelemetryUpdated;
            _provider.OnRequestsPerSecondUpdated -= Provider_OnRequestsPerSecondUpdated;
        }
    }
}
