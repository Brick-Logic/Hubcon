using ExampleMicroservice1.ContractHandlers;
using ExampleMicroservicesDomain.Middlewares;
using Hubcon.Server.Injection;
using Hubcon.Client;
using ExampleMicroservice1.ServerModules;
using Hubcon.Server.Abstractions.Interfaces;
using Scalar;
using Scalar.AspNetCore;

namespace ExampleMicroservice1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();

            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<Microservice2ServerModule>();
            builder.AddHubconServer();
            builder.ConfigureHubconServer(serverOptions =>
            {
                serverOptions.AddGlobalMiddleware<ExceptionMiddleware>();

                serverOptions.AddController<ExampleMicroservice1ContractHandler>();
            });

            builder.Services.AddLogging();

            var app = builder.Build();

            app.MapOpenApi();
            app.MapScalarApiReference();

            app.MapHubconControllers();

            app.Run();
        }
    }
}
