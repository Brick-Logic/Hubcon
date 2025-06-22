using ExampleMicroservice3.ContractHandlers;
using ExampleMicroservice3.ServerModules;
using ExampleMicroservicesDomain.Middlewares;
using Hubcon.Server.Injection;
using Hubcon.Client;
using ExampleMicroservicesDomain;

namespace ExampleMicroservice3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<Microservice1ServerModule>();

            builder.AddHubconServer();
            builder.ConfigureHubconServer(controllerOptions =>
            {
                controllerOptions.AddGlobalMiddleware<ExceptionMiddleware>();

                controllerOptions.AddController<ExampleMicroservice3ContractHandler>(controllerMiddlewares =>
                {
                    controllerMiddlewares.UseGlobalMiddlewaresFirst(true);
                    //controllerMiddlewares.AddMiddleware<LocalLoggingMiddleware>();
                });
            });

            builder.Services.AddLogging();

            var app = builder.Build();

            var initialScope = app.Services.CreateScope();
            var microservice1 = initialScope.ServiceProvider.GetRequiredService<IExampleMicroservice1Contract>();
            var logger = initialScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Waiting to start...");
            Console.ReadKey();

            microservice1.ProcessMessage("My custom message");

            app.MapHubconControllers();
            app.UseHubconWebsockets();

            app.Run();
        }
    }
}