using ExampleMicroservice3.ContractHandlers;
using ExampleMicroservice3.ServerModules;
using ExampleMicroservicesDomain.Middlewares;
using Hubcon.Server.Injection;
using Hubcon.Client;
using ExampleMicroservicesDomain;
using Scalar.AspNetCore;

namespace ExampleMicroservice3
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();

            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<Microservice1ServerModule>();

            builder.AddHubconServer();
            builder.ConfigureHubconServer(controllerOptions =>
            {
                controllerOptions.AddGlobalMiddleware<ExceptionMiddleware>();

                controllerOptions.AddController<ExampleMicroservice3ContractHandler>(controllerMiddlewares =>
                {
                    controllerMiddlewares.UseGlobalMiddlewaresFirst(true);
                });
            });

            builder.Services.AddLogging();

            var app = builder.Build();

            app.MapOpenApi();
            app.MapScalarApiReference();

            app.UseHubconHttpEndpoints();

            var initialScope = app.Services.CreateScope();
            var microservice1 = initialScope.ServiceProvider.GetRequiredService<IExampleMicroservice1Contract>();
            var logger = initialScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Waiting to start...");
            Console.ReadKey();

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    logger.LogInformation("Sending message to microservice 1...");
                    await microservice1.ProcessMessage("My custom message.");
                }
            });
                     
            app.Run();
        }
    }
}