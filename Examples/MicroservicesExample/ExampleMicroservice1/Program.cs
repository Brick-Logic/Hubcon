using ExampleMicroservice1.ServerModules;
using Hubcon;
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
                serverOptions.AutoRegisterControllers();
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
