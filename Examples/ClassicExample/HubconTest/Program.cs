using Hubcon;
using Hubcon.Server.Abstractions.CustomAttributes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;

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

            process.ProcessorAffinity = (IntPtr)coreMask;
            process.PriorityClass = ProcessPriorityClass.RealTime;

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

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxConcurrentConnections = null;
                options.Limits.MaxConcurrentUpgradedConnections = null;
            });

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

            builder.Services.AddSingleton(tokenValidationParameters);

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddJwtBearer(options =>
               {
                   options.TokenValidationParameters = tokenValidationParameters;
               });

            builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();
            builder.AddHubconServer();
            builder.ConfigureHubconServer(serverOptions =>
            {
                serverOptions.AddAuthentication();
                serverOptions.AddTelemetry();

                serverOptions.AddHttpRateLimiter(options =>
                {
                    options.AddPolicy("contract", httpContext =>
                    {
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            factory: x => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromSeconds(1),
                                AutoReplenishment = true,
                                QueueLimit = 20,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                            });
                    });

                    options.AddPolicy("endpoint", httpContext =>
                    {
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            factory: x => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromSeconds(1),
                                AutoReplenishment = true,
                                QueueLimit = 20,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                            });
                    });
                });

                serverOptions.ConfigureCore(config =>
                {
                    config.SetMaxConcurrentOperations(5000000);

                    config.UseWebsocketTokenHandler((token, serviceProvider) =>
                    {
                        var user = JwtHelper.ValidateJwtToken(token, tokenValidationParameters, out var validatedToken);

                        DateTime? expiration = null;

                        var expClaim = user?.FindFirst("exp")?.Value;

                        if (expClaim == null)
                            return null;

                        // Convierte de segundos desde epoch a DateTime
                        if (long.TryParse(expClaim, out var expSeconds))
                        {
                            var dateTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                            expiration = dateTime;
                        }

                        if (user == null || expiration == null)
                            return null;

                        return (user, expiration.Value);
                    })
                    .EnableWebsocketsLogging()
                    .DisableAllRateLimiters()
                    .EnableRequestDetailedErrors();
                });

                serverOptions.AutoRegisterControllers();
            });

            var app = builder.Build();

            app.UseCors();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseRateLimiter();

            app.UseAuthentication(); // debe ir antes de UseAuthorization
            app.UseAuthorization();

            app.UseHubconHttpEndpoints();

            //var options = new WebSocketOptions();
            //options.AllowedOrigins.Add("http://localhost:5000");
            app.UseHubconWebsocketEndpoints();

            var logger = app.Services.GetService<ILogger<object>>();

            Watcher.Start(logger!);

            var telemetry = app.Services.GetRequiredService<ITelemetryService>();

            telemetry.OnRequestsPerSecondUpdated += (telemetry, rps) =>
            {
                Console.Title = $" RPS: {rps.RequestsPerSecond} | CPU: {telemetry.CurrentCPU} | Threads: {telemetry.CurrentThreads} | WS clients: {telemetry.CurrentWebSocketClients} | WS req/s: {rps.WebSocketsRequestsPerSecond} | HTTP req/s: {rps.HttpRequestsPerSecond}"; 
            };

            app.Run();
        }
    }
}