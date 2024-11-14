using Hubcon;
using TestAPI.HubControllers;
using TestDomain;

namespace TestAPI
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHubcon();
            builder.Services.AddScoped<ClientHubControllerConnector<ITestClientController, TestServerHubController>>();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();
            app.MapHub<TestServerHubController>("/clienthub");

            //Just a test endpoint, it can also be injected in a controller.
            app.MapGet("/test", async (ClientHubControllerConnector<ITestClientController, TestServerHubController> client) =>
            {
                // Getting some connected clientId
                var clientId = ServerHub<ITestServerHubController>.GetClients().FirstOrDefault()!.Id;

                // Gets a client instance
                var instance = client.GetInstance(clientId);

                // Using some methods
                await instance.ShowText();
                var temperature = await instance.GetTemperature();

                return temperature.ToString();
            });

            app.Run();
        }
    }
}
