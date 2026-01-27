using ExampleMicroservices.Shared.Middlewares;
using ExampleMicroservicesDomain;
using Hubcon;

namespace ExampleMicroservice1.ContractHandlers
{
    [UseMiddleware<ExceptionMiddleware>]
    public class ExampleMicroservice1ContractHandler(
        IExampleMicroservice2Contract microservice2,
        ILogger<ExampleMicroservice1ContractHandler> logger) : IExampleMicroservice1Contract
    {
        public async Task ProcessMessage(string message)
        {
            logger.LogInformation($"[Microservice 1] Got message: '{message}'. Sending to microservice 2...");
            await Task.Delay(1000);
            await microservice2.ProcessMessage(message);
        }

        public Task FinishMessage(string message)
        {
            logger.LogInformation($"[Microservice 1] Got message: '{message}'.");
            return Task.CompletedTask;
        }
    }
}