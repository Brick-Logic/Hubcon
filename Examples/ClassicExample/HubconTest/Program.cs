using System.Collections.Concurrent;
using Hubcon;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hubcon.Server.Core.Telemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HubconTest
{
    public static class Watcher
    {
        static System.Timers.Timer worker;

        public static void Start(ILogger<object> logger)
        {
            var process = Process.GetCurrentProcess();

            long coreMask = 0;

            int? customCores = null;
            int cores = customCores ?? Environment.ProcessorCount - 1;

            for (int i = 0; i <= cores; i++)
            {
                coreMask |= 1L << i;
            }

            // process.ProcessorAffinity = (IntPtr)coreMask;
            // process.PriorityClass = ProcessPriorityClass.RealTime;

            //worker = new System.Timers.Timer();
            //worker.Interval = 1000;
            //worker.Elapsed += (sender, eventArgs) =>
            //{
            //    ThreadPool.GetAvailableThreads(out var workerThreads, out _);
            //    logger.LogInformation("Threads disponibles: " + workerThreads);
            //};
            //worker.Start();

            //var heap = Task.Run(async () =>
            //{
            //    var sw = Stopwatch.StartNew();
            //    while (true)
            //    {
            //        var allocated = GC.GetTotalMemory(forceFullCollection: false);
            //        Console.WriteLine($"Heap Size: {allocated / 1024.0 / 1024.0:N2} MB - Time: {sw.Elapsed}");
            //        await Task.Delay(1000);
            //    }
            //});

            //var gc = Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        GC.Collect(2, GCCollectionMode.Forced, blocking: false);
            //        GC.WaitForPendingFinalizers();
            //        await Task.Delay(60000);
            //    }
            //});
        }
    }

    public class Program
    {
        public static string Key = "cITTqWy43KvkXYrBjvX9YTgs/wVo0qVJ2oXIiknta+k=";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // builder.WebHost.ConfigureKestrel(options =>
            // {
            //     options.Limits.MaxConcurrentConnections = null;
            //     options.Limits.MaxConcurrentUpgradedConnections = null;
            //     options.Limits.MinRequestBodyDataRate = null; // Evita desconexiones por lentitud en tests
            // });
            //
            
            // builder.Services.AddOpenTelemetry()
            //     .ConfigureResource(resource => resource.AddService("Hubcon"))
            //     .WithTracing(tracing =>
            //     {
            //         tracing.AddSource(OpenTelemetryBatchWorker.HubconActivitySource.Name);
            //         tracing.SetSampler(new AlwaysOnSampler());
            //         tracing.AddOtlpExporter(options =>
            //         {
            //             options.Endpoint = new Uri("http://localhost:4317");
            //             options.Protocol = OtlpExportProtocol.Grpc;
            //         });
            //     });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "clave",
                ValidAudience = "clave",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
            };

            builder.AddHubconServer(serverOptions =>
            {
                serverOptions.AddAuthentication();
                serverOptions.AddTelemetry();
                serverOptions.AddOpenTelemetry();
                serverOptions.AddConcurrencyLimiter();
                
                serverOptions.UseTokenValidationParameters(tokenValidationParameters);

                serverOptions.ConfigureCore(config =>
                {
                    config
                        .SetMaxConcurrentOperations(999999)
                        .SetGlobalRateLimiter(999999)
                        .AddSetting("", new object())
                        .AddTransportAuth<WebSocketTransport, JwtAuthHandler>()
                        .EnableWebsocketsLogging()
                        .AllowRemoteTokenCancellation();
                });

                serverOptions.AutoRegisterControllers();
            });

            builder.Services.AddOpenApi();
            
            var app = builder.Build();

            app.UseCors();

            app.MapOpenApi();
            app.MapScalarApiReference();

            app.UseHubconHttpEndpoints();
            app.UseHubconWebsocketEndpoints();

            var logger = app.Services.GetService<ILogger<object>>();

            // Watcher.Start(logger!);

            var telemetry = app.Services.GetRequiredService<ITelemetryService>();
            telemetry.OnRequestsPerSecondUpdated += (telemetry, rps) =>
            {
                var totalRps = rps.Snapshots
                    .Select(x => x.Value)
                    .Sum(x => x.Calls + x.Invokes + x.StreamingsRequests + x.IngestsRequests);

                Interlocked.Add(ref TotalRequests, totalRps);
                
                var title = "";
                title += $"| Total RPS: {totalRps.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))}\n" +
                         $"| Total requests: {TotalRequests.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))}\n" +
                         $"| Current WebSocket Connections: {telemetry.CurrentWebSocketClients.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))}\n" +
                         $"| CPU usage: {telemetry.CurrentCPU:F2}%\n" +
                         $"| Heap memory: {telemetry.CurrentHeapSize / (1024.0 * 1024.0):F2} MB\n" +
                         $"| Threads: {telemetry.CurrentThreads}\n";

                foreach (var transport in rps.Snapshots)
                {
                    var total = transport.Value.Calls + transport.Value.Invokes + transport.Value.StreamingsRequests + transport.Value.IngestsRequests;
                    var totalTransportRequests = TotalRequestsPerTransport.AddOrUpdate(transport.Key, total, (x, y) => y + total);
                    
                    title += $"[{transport.Key.GetType().Name}] " +
                             $"Total: {totalTransportRequests.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} " +
                             $"| Total current: {transport.Value.Calls.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} " +
                             $"| Calls: {transport.Value.Calls.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} " +
                             $"| Invokes: {transport.Value.Invokes.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} " +
                             $"| Streams: {transport.Value.StreamingsRequests.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} " +
                             $"| Ingests: {transport.Value.IngestsRequests.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))}\n";
                }
                
                Console.Title = title;
                logger.LogInformation(title);
            };

            app.Run();
        }

        static long TotalRequests = 0;
        private static ConcurrentDictionary<HubconTransportAttribute, long> TotalRequestsPerTransport = new();
    }
}