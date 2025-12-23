using BlazorTestServer.Controllers;
using BlazorTestServer.Middlewares;
using Hubcon.Server.Injection;
using Hubcon.Shared.Core.Tools;
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

            builder.Services.AddOpenApi();

            builder.AddHubconServer();
            builder.ConfigureHubconServer(serverOptions =>
            {
                serverOptions.ConfigureCore(coreOptions =>
                {
                    coreOptions.SetWebSocketTimeout(TimeSpan.FromSeconds(15));

                    coreOptions
                        .UseWebsocketTokenHandler((token, serviceProvider) =>
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
                        .DisableAllRateLimiters()
                        .EnableRequestDetailedErrors();
                });

                serverOptions.AddGlobalMiddleware<ExceptionMiddleware>();
                serverOptions.AddController<TestController>();
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

            app.MapControllers();

            app.UseHubconHttpEndpoints();
            app.UseHubconWebsocketEndpoints();

            app.Run();
        }
    }
}
