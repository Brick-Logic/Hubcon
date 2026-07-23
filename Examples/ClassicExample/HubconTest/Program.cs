using Hubcon;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HubconTest
{
    public static class Watcher
    {
        static System.Timers.Timer worker;

        public static void Start(ILogger<object> logger)
        {
            var process = Process.GetCurrentProcess();

            long coreMask = 0;

            int? customCores = 11;
            int cores = customCores ?? Environment.ProcessorCount - 1;

            for (int i = 6; i <= cores; i++)
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
                //serverOptions.AddConcurrencyLimiter();
                serverOptions.UseTokenValidationParameters(tokenValidationParameters);
                
                serverOptions.ConfigureCore(config =>
                {
                        config
                            //.SetMaxConcurrentOperations(999999)
                            //.SetGlobalRateLimiter(999999)
                            .AddTransportAuth<WebSocketTransport, JwtAuthHandler>()
                            .EnableWebsocketsLogging()  
                            .DisableWebsocketPing()
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
                var title = $" RPS: {rps.RequestsPerSecond.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} | Total requests: {TotalRequests.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} | CPU: {telemetry.CurrentCPU} | Threads: {telemetry.CurrentThreads} | WS clients: {telemetry.CurrentWebSocketClients} | WS req/s: {rps.WebSocketsRequestsPerSecond} | HTTP req/s: {rps.HttpRequestsPerSecond}";
                Console.Title = title;
                Interlocked.Add(ref TotalRequests, rps.RequestsPerSecond);
                logger.LogInformation(title);
            };

            app.Run();
        }

        static long TotalRequests = 0;
    }
}