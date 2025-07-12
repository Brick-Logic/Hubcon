using Hubcon.Server.Injection;
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
            for (int i = 1; i <= Environment.ProcessorCount-1; i++)
            {
                coreMask |= 1L << i;
            }

            process.ProcessorAffinity = (IntPtr)coreMask;
            process.PriorityClass = ProcessPriorityClass.RealTime;

            worker = new System.Timers.Timer();
            worker.Interval = 1000;
            worker.Elapsed += (sender, eventArgs) =>
            {
                ThreadPool.GetAvailableThreads(out var workerThreads, out _);
                logger.LogInformation("Threads disponibles: " + workerThreads);
            };
            worker.Start();
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

            builder.AddHubconServer();
            builder.ConfigureHubconServer(serverOptions =>
            {
                serverOptions.ConfigureCore(config => 
                {
                    config
                    .ThrottleWebsocketIngest(TimeSpan.Zero)
                    .ThrottleWebsocketMethods(TimeSpan.Zero)
                    .ThrottleWebsocketSubscription(TimeSpan.Zero)
                    .ThrottleWebsocketStreaming(TimeSpan.Zero)
                    .EnableRequestDetailedErrors();
                    //.SetHttpPathPrefix("prefix1")
                    //.SetWebSocketPathPrefix("wsprefix");                   
                });

                serverOptions.AddController<UserContractHandler>();
                serverOptions.AddController<SecondTestController>();
            });

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "clave",
                        ValidAudience = "clave",
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
                    };
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
