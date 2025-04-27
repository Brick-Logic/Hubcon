using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Server;
using Hubcon.SignalR.Server;
using HubconTestDomain;

namespace HubconTest.Controllers
{
    public class TestController : ITestContract
    {
        public ISubscriptionHandler<int>? OnEventCreated { get; }

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
            Console.WriteLine(message);
            await Task.CompletedTask;
        }

        public async Task ShowTempOnServerFromClient()
        {
            await Task.CompletedTask;
        }

        public Task ShowTextOnServer()
        {
            Console.WriteLine("Mostrando texto");
            return Task.CompletedTask;
        }
    }
}
