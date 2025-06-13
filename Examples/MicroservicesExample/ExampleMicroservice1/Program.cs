using ExampleMicroservice1.ContractHandlers;
using ExampleMicroservicesDomain.Middlewares;
using Hubcon.Server.Injection;
using Hubcon.Client;
using ExampleMicroservice1.ServerModules;

namespace ExampleMicroservice1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<Microservice2ServerModule>();

            builder.AddHubconServer();
            builder.ConfigureHubconServer(controllerOptions =>
            {
                controllerOptions.AddGlobalMiddleware<ExceptionMiddleware>();

                controllerOptions.AddController<ExampleMicroservice1ContractHandler>(controllerMiddlewares =>
                {
                    controllerMiddlewares.UseGlobalMiddlewaresFirst(true);
                    //controllerMiddlewares.AddMiddleware<LocalLoggingMiddleware>();
                });
            });

            builder.Services.AddLogging();

            var app = builder.Build();

            app.UseHubcon();

            app.Run();
        }
    }
}
