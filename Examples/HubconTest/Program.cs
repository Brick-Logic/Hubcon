using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Core.Builders;
using Hubcon.GraphQL.Injection;
using HubconTest.Controllers;
using HubconTestDomain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

namespace HubconTest
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

            builder.Services.AddOpenApi();

            builder.AddHubconGraphQL(controllerOptions =>
            {
                controllerOptions.AddGlobalMiddleware<LoggingMiddleware>();
                controllerOptions.AddGlobalMiddleware<AuthenticationMiddleware>();

                controllerOptions.AddController<TestController>(controllerMiddlewares =>
                {
                    //controllerMiddlewares.AddMiddleware<LoggingMiddleware>();
                });
            });

            builder.Services.AddHttpContextAccessor();
            builder.UseContractsFromAssembly(nameof(HubconTestDomain));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "clave",
                        ValidAudience = "clave",
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("clave"))
                    };
                });

            builder.Services.AddAuthorization(); // también necesaria

            //builder.UseHubconSignalR();
            //builder.AddHubconController<TestSignalRController>(options =>
            //{
            //    options.AddMiddleware<LoggingMiddleware>();
            //});

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseAuthorization();

            app.MapControllers();
            //app.MapHub<TestSignalRController>("/clienthub");

            app.MapHubconControllers();
            app.MapHubconGraphQL("/graphql");

            //app.MapHubconRestControllers();

            //Just a test endpoint, it can also be injected in a controller.
            app.MapGet("/test", async (IClientAccessor<ITestClientController> clientAccessor) =>
            {
                // Getting some connected clientId
                var clientId = clientAccessor.GetAllClients().FirstOrDefault()!;

                //Gets a client instance
                var client = clientAccessor.GetOrCreateClient(clientId);

                Console.WriteLine(await client.GetTemperature());

                //var test = client.ShowAndReturnMessage("hello");

                //var messages = client.GetMessages(10);

                //await foreach (var item in messages)
                //{
                //    Console.WriteLine(item);
                //}
            });

            app.Run();
        }
    }
}
