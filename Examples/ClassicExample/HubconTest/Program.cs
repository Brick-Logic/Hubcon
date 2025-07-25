using Hubcon.Server.Injection;
using Hubcon.Shared.Core.Tools;
using HubconTest.ContractHandlers;
using HubconTest.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Diagnostics;
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

            var heap = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                while (true)
                {
                    var allocated = GC.GetTotalMemory(forceFullCollection: false);
                    Console.WriteLine($"Heap Size: {allocated / 1024.0 / 1024.0:N2} MB - Time: {sw.Elapsed}");
                    await Task.Delay(1000);
                }
            });
        }
    }

    public class Program
    {
        public static string Key = "cITTqWy43KvkXYrBjvX9YTgs/wVo0qVJ2oXIiknta+k=";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .AllowAnyOrigin() // Solo para desarrollo
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            builder.Services.AddOpenApi();

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

            builder.AddHubconServer();
            builder.ConfigureHubconServer(serverOptions =>
            {
                serverOptions.ConfigureCore(config => 
                {
                    config
                    .UseWebsocketTokenHandler((token, serviceProvider) =>
                    {
                        return JwtHelper.ValidateJwtToken(token, tokenValidationParameters, out var validatedToken);
                    })
                    .SetHttpTimeout(TimeSpan.FromSeconds(15))
                    .SetWebSocketTimeout(TimeSpan.FromSeconds(120))
                    .SetMaxHttpMessageSize(4 * 1024)
                    .SetMaxWebSocketMessageSize(4 * 1024)
                    .EnableRequestDetailedErrors()
                    .DisableAllThrottling();
                });

                serverOptions.AddController<UserContractHandler>();
                serverOptions.AddController<SecondTestController>();
            });     

            builder.Services.AddAuthorization();

            var app = builder.Build();

            app.UseCors();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseAuthentication(); // debe ir antes de UseAuthorization
            app.UseAuthorization();

            app.MapHubconControllers();
            app.UseHubconWebsockets();

            var logger = app.Services.GetService<ILogger<object>>();

            Watcher.Start(logger!);

            app.Run();
        }
    }
}
