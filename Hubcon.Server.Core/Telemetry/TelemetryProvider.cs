using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Core.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Hubcon.Server.Core.Telemetry
{
    internal class TelemetryProvider : ITelemetryProvider
    {
        public event Action? OnTelemetryUpdated;
        public event Action<IRequestsPerSecondSnapshot>? OnRequestsPerSecondUpdated;

        public void RegisterProvider<T>(Expression<Func<ITelemetryProvider, T>> providerProperty, T provider)
        {
            if (!(providerProperty.Body is MemberExpression memberExpr && (memberExpr.Member is PropertyInfo)))
            {
                return;
            }

            var prop = memberExpr.Member as PropertyInfo;
            PropertyTools.AssignProperty(this, prop.Name, provider);
        }

        public void CallOnRequestsPerSecondUpdated(IRequestsPerSecondSnapshot snapshot) => OnRequestsPerSecondUpdated?.Invoke(snapshot);
        public void CallOnTelemetryUpdated() => OnTelemetryUpdated?.Invoke();

        // Process telemetry
        public Func<double>? GetCurrentCPU { get; }
        public Func<Process>? GetCurrentProcess { get; }
        public Func<double>? GetCurrentHeapSize { get; }
        public Func<int>? GetThreadCount { get; }

        // Websockets telemetry
        public Func<int>? GetCurrentWebsocketClients { get; }
        public Func<int>? CurrentSubscriptionCount { get; }
        public Func<int>? CurrentIngestCount { get; }
        public Func<int>? CurrentStreamingsCount { get; }
        public Func<int>? CurrentWebSocketsRequestsCount { get; }
        public Func<int>? CurrentWebSocketsRoundTripRequestsCount { get; }
        public Func<int>? CurrentWebSocketsCallRequestsCount { get; }

        // HTTP telemetry
        public Func<int>? ActiveHttpRequestsCount { get; }
        public Func<int>? CurrentHttpRoundTripRequestsCount { get; }
        public Func<int>? CurrentHttpCallRequestsCount { get; }

        // RPS
        public Func<int>? CurrentRequestsPerSecond { get; }

        public Func<int>? CurrentSubscriptionPerSecond { get; }
        public Func<int>? CurrentStreamingsPerSecond { get; }
        public Func<int>? CurrentIngestPerSecond { get; }

        public Func<int>? CurrentWebSocketsRequestsPerSecond { get; }
        public Func<int>? CurrentWebSocketsCallRequestsPerSecond { get; }
        public Func<int>? CurrentWebSocketsRoundTripRequestsPerSecond { get; }

        public Func<int>? CurrentHttpRequestsPerSecond { get; }
        public Func<int>? CurrentHttpCallRequestsPerSecond { get; }
        public Func<int>? CurrentHttpRoundTripRequestsPerSecond { get; }

    }
}
