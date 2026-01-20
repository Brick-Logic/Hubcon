using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core
{
    public static class DependencyInjection
    {
        private static System.Timers.Timer worker;
        private static DateTime _lastCheckTime = DateTime.UtcNow;
        private static TimeSpan _lastProcessorTime = TimeSpan.Zero;

        private static long allocated;
        private static double cpuUsage;
        private static int threads;
        private static readonly Process _currentProcess = Process.GetCurrentProcess();

        public static WebApplicationBuilder AddServerCore(this WebApplicationBuilder builder)
        {
            var telemetryProvider = new TelemetryProvider();

            worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs) =>
            {
                UpdateCpuTitle();
                telemetryProvider.CallOnTelemetryUpdated();
            };
            worker.Start();

            var process = Process.GetCurrentProcess();

            telemetryProvider.RegisterProvider(x => x.GetCurrentProcess, () => process);
            telemetryProvider.RegisterProvider(x => x.GetCurrentCPU, () => cpuUsage);
            telemetryProvider.RegisterProvider(x => x.GetCurrentHeapSize, () => allocated);
            telemetryProvider.RegisterProvider(x => x.GetThreadCount, () => threads);

            builder.Services.AddSingleton<ITelemetryProvider>(x => telemetryProvider);
            builder.Services.AddSingleton<ITelemetryService, TelemetryService>();
            return builder;
        }

        public static void UpdateCpuTitle()
        {
            var currentTime = DateTime.UtcNow;
            var currentProcessorTime = _currentProcess.TotalProcessorTime;
            allocated = GC.GetTotalMemory(forceFullCollection: false);

            // Calculamos cuánto tiempo de CPU se usó en este intervalo de 1 segundo
            cpuUsage = (currentProcessorTime - _lastProcessorTime).TotalMilliseconds /
                              (currentTime - _lastCheckTime).TotalMilliseconds /
                              Environment.ProcessorCount * 100;

            threads = ThreadPool.ThreadCount;

            _lastCheckTime = currentTime;
            _lastProcessorTime = currentProcessorTime;
        }
    }
}
