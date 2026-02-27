using ExampleMicroservices.Shared.Middlewares;
using ExampleMicroservicesDomain;
using Hubcon;
using Microsoft.AspNetCore.Authorization;

namespace ExampleMicroservice2.ContractHandlers
{
    [UseMiddleware<ExceptionMiddleware>]
    [AllowAnonymous]
    public class ExampleMicroservice2ContractHandler(
        IExampleMicroservice3Contract microservice3, 
        ILogger<ExampleMicroservice2ContractHandler> logger) : IExampleMicroservice2Contract
    {
        public async Task ProcessMessage(string message)
        {
            logger.LogInformation($"[Microservice 2] Got message: '{message}'. Sending to microservice 3...");
            await Task.Delay(1000);
            await microservice3.ProcessMessage(message);
        }
    }
}