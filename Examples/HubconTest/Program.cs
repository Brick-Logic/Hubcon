using Hubcon.Core;
using Hubcon.Core.Models.Interfaces;
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

            builder.UseHubconSignalR();
            builder.Services.AddHubconClientAccessor();
            builder.Services.AddHubconController<TestSignalRController>();


            var app = builder.Build();

            app.UseHubcon();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseAuthorization();

            app.MapControllers();

            app.MapHub<TestSignalRController>("/clienthub");

            //Just a test endpoint, it can also be injected in a controller.
            app.MapGet("/test", async (IClientManager<ITestClientController, TestSignalRController> clientAccessor) =>
            {
                // Getting some connected clientId
                var clientId = clientAccessor.GetAllClients().FirstOrDefault()!;

                // Gets a client instance
                var client = clientAccessor.GetOrCreateClient(clientId);
                var messages = client.GetMessages(5).ToBlockingEnumerable();

                foreach (var item in messages)
                {
                    Console.WriteLine(item);                   
                }



                //await foreach (var item in messages)
                //{
                //    Console.WriteLine(item);
                //}

                //// Using some methods
                //string message = "Mensaje de prueba";
                //Console.WriteLine($"Servidor: Mensaje enviado a cliente de Hubcon SignalR: {message}");
                //var item = await instance.ShowAndReturnMessage(message);
                //Console.WriteLine($"Servidor: Mensaje recibido desde el cliente de Hubcon SignalR: {message}");
            });

            app.Run();
        }
    }
}
