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
    [HubconPreserve]
    public sealed class TelemetryProvider : ITelemetryProvider
    {
        private static readonly int Processors = Environment.ProcessorCount;
        private static readonly Process _process = Process.GetCurrentProcess();
        private static TimeSpan _lastCpuTime = _process.TotalProcessorTime;
        private static long _lastSnapshotTicks = Stopwatch.GetTimestamp();
        
        public TelemetryProvider()
        {
            
        }
        
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
        
        private static double GetCpuUsagePercentage()
        {
            var currentCpuTime = _process.TotalProcessorTime;
            var currentTicks = Stopwatch.GetTimestamp();

            var cpuUsed = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var timePassed = (double)(currentTicks - _lastSnapshotTicks) / Stopwatch.Frequency * 1000;

            _lastCpuTime = currentCpuTime;
            _lastSnapshotTicks = currentTicks;

            if (timePassed <= 0) return 0;

            return (cpuUsed / (timePassed * Processors)) * 100;
        }

        public Func<double>? GetCurrentCPU { get; set; } =  GetCpuUsagePercentage;
        public Func<Process>? GetCurrentProcess { get; set; } = static () => _process;
        public Func<double>? GetCurrentHeapSize { get; set; } = static () => GC.GetTotalMemory(forceFullCollection: false);
        public Func<int>? GetThreadCount { get; set; } = static () => ThreadPool.ThreadCount;
        
        public Func<int>? GetCurrentWebsocketClients { get; }

    }
}
