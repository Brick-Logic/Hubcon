using ExampleMicroservicesDomain;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ExampleMicroservice2.ContractHandlers
{
    public class ExampleMicroservice2ContractHandler(IExampleMicroservice3Contract microservice3, ILogger<ExampleMicroservice2ContractHandler> logger) : IExampleMicroservice2Contract
    {
        public async Task ProcessMessage(string message)
        {
            logger.LogInformation($"[Microservice 2] Got message: '{message}'. Sending to microservice 3...");
            //await Task.Delay(1000);
            await microservice3.ProcessMessage(message);
        }
    }
}