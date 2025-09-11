using ExampleMicroservice2.ContractHandlers;
using ExampleMicroservice2.ServerModules;
using ExampleMicroservicesDomain.Middlewares;
using Hubcon.Server.Injection;
using Hubcon.Client;
using Scalar.AspNetCore;

namespace ExampleMicroservice2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();

            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<Microservice3ServerModule>();

            builder.AddHubconServer();
            builder.ConfigureHubconServer(controllerOptions =>
            {
                controllerOptions.AddGlobalMiddleware<ExceptionMiddleware>();

                controllerOptions.AddController<ExampleMicroservice2ContractHandler>(controllerMiddlewares =>
                {
                    controllerMiddlewares.UseGlobalMiddlewaresFirst(true);
                    //controllerMiddlewares.AddMiddleware<LocalLoggingMiddleware>();
                });
            });

            builder.Services.AddLogging();

            var app = builder.Build();

            app.MapOpenApi();
            app.MapScalarApiReference();

            app.UseHubconHttpEndpoints();

            app.Run();
        }
    }
}