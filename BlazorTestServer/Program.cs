using BlazorTestServer.Controllers;
using BlazorTestServer.Middlewares;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Injection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

namespace BlazorTestServer
{
    public class Program
    {
        public static string Key = "cITTqWy43KvkXYrBjvX9YTgs/wVo0qVJ2oXIiknta+k=";

        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

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
                serverOptions.ConfigureCore(coreOptions =>
                    coreOptions
                        .SetWebSocketTimeout(TimeSpan.FromSeconds(15))
                        .SetHttpTimeout(TimeSpan.FromSeconds(15))
                        .AllowWebSocketIngest()
                        .AllowWebSocketSubscriptions()
                        .AllowWebSocketNormalMethods()
                        .RequirePing()
                        .EnableWebSocketPong()
                );

                serverOptions.AddGlobalMiddleware<ExceptionMiddleware>();
                serverOptions.AddController<TestController>();
                serverOptions.AddController<SecondTestController>();
            });

            //builder.UseContractsFromAssembly(nameof(HubconTestDomain));

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

            app.MapControllers();

            app.MapHubconControllers();
            app.UseHubconWebsockets();

            app.Run();
        }
    }
}
