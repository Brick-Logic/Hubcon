using Hubcon.Core;
using Hubcon.Core.Middleware;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL;
using Hubcon.SignalR;
using HubconTest.Controllers;
using HubconTestDomain;
using Scalar.AspNetCore;

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
            //builder.AddHubconGraphQL(controllerOptions => 
            //{
            //    controllerOptions.AddController<TestSignalRController>();
            //});
            builder.UseHubconSignalR();
            builder.AddHubconController<TestSignalRController>(options =>
            {
                options.AddMiddleware<LoggingMiddleware>();
            });
            builder.AddContractsFromAssembly(nameof(HubconTestDomain));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseAuthorization();

            app.MapControllers();

            //app.MapHubconGraphQL("/graphql");

            app.MapHub<TestSignalRController>("/clienthub");

            //app.MapHubconRestControllers();

            //Just a test endpoint, it can also be injected in a controller.
            app.MapGet("/test", async (IClientAccessor<ITestClientController, TestSignalRController> clientAccessor) =>
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
