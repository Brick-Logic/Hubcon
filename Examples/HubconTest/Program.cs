using Hubcon.Server.Injection;
using HubconTest.Controllers;
using HubconTest.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

namespace HubconTest
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

            builder.AddHubcon(controllerOptions =>
            {
                //controllerOptions.AddGlobalMiddleware<GlobalLoggingMiddleware>();
                controllerOptions.AddGlobalMiddleware<ExceptionMiddleware>();
                controllerOptions.AddGlobalMiddleware<GlobalLoggingMiddleware>();
                //controllerOptions.AddGlobalMiddleware<AuthenticationMiddleware>();

                controllerOptions.AddController<TestController>(controllerMiddlewares =>
                {
                    //controllerMiddlewares.AddMiddleware<LocalLoggingMiddleware>();
                    controllerMiddlewares.UseGlobalMiddlewaresFirst(true);
                });

                controllerOptions.AddController<SecondTestController>(controllerMiddlewares =>
                {
                    //controllerMiddlewares.AddMiddleware<LocalLoggingMiddleware>();
                    controllerMiddlewares.UseGlobalMiddlewaresFirst(true);
                });
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

            app.MapHubconGraphQL("/graphql");

            app.Run();
        }
    }
}
