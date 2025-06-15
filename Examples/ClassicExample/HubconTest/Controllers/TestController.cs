using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace HubconTest.Controllers
{
    public class TestController(ILogger<TestController> logger) : IUserService
    {
        [AllowAnonymous]
        public ISubscription<int>? OnUserCreated { get; }

        public async Task<int> GetTemperatureFromServer() 
            => await Task.Run(() => new Random().Next(-10, 50));

        public async IAsyncEnumerable<string> GetMessages(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return await Task.Run(() => "hola2");
            }
        }

        public async Task PrintMessage(string message)
        {
            logger.LogInformation(message);
            await Task.CompletedTask;
        }

        //[Authorize(Roles = ["Admin"])]
        public async Task CreateUser()
        {
            var number = Random.Shared.Next(-10, 50);
            OnUserCreated?.Emit(number);
            await Task.CompletedTask;
        }

        public Task ShowTextOnServer()
        {
            logger.LogInformation("Mostrando texto");
            return Task.CompletedTask;
        }

        public async Task IngestMessages(IAsyncEnumerable<string> source, IAsyncEnumerable<string> source2)
        {
            var task1 = Task.Run(async () =>
            {
                await foreach(var item in source)
                {
                    logger.LogInformation($"source1: {item}");
                }
            });

            var task2 = Task.Run(async () =>
            {
                await foreach (var item in source2)
                {
                    logger.LogInformation($"source2: {item}");
                }
            });

            await Task.WhenAll(task1, task2);
            logger.LogInformation("Ingest terminado exitosamente");
        }
    }
}
