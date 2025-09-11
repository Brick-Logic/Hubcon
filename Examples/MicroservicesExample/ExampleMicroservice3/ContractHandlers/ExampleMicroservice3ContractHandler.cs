using ExampleMicroservicesDomain;
using ExampleMicroservicesDomain.Middlewares;
using Hubcon.Server.Core.Middlewares;

namespace ExampleMicroservice3.ContractHandlers
{
    [UseMiddleware(typeof(ExceptionMiddleware))]
    public class ExampleMicroservice3ContractHandler(
        IExampleMicroservice1Contract microservice1, 
        ILogger<ExampleMicroservice3ContractHandler> logger) : IExampleMicroservice3Contract
    {
        public async Task ProcessMessage(string message)
        {
            logger.LogInformation($"[Microservice 3] Got message: '{message}'. Sending to microservice 1...");
            await Task.Delay(1000);
            await microservice1.FinishMessage(message);
        }
    }
}