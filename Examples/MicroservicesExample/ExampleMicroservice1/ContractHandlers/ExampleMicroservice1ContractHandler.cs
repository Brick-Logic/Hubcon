using ExampleMicroservicesDomain;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ExampleMicroservice1.ContractHandlers
{
    public class ExampleMicroservice1ContractHandler(
        IExampleMicroservice2Contract microservice2, 
        ILogger<ExampleMicroservice1ContractHandler> logger) : IExampleMicroservice1Contract
    {
        public async Task ProcessMessage(string message)
        {
            while(true)
            {
                //logger.LogInformation($"[Microservice 1] Got message: '{message}'. Sending to microservice 2...");
                //await Task.Delay(1000);
                 await microservice2.ProcessMessage(message);
            }
        }

        public Task FinishMessage(string message)
        {
            logger.LogInformation($"[Microservice 1] Got message: '{message}'.");
            return Task.CompletedTask;
        }
    }
}