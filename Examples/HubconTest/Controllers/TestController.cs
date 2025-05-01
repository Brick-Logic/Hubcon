using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using HubconTestDomain;

namespace HubconTest.Controllers
{
    public class TestController : ITestContract
    {
        public ISubscription OnEventCreated { get; }

        public TestController()
        {
            
        }

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
            OnEventCreated?.Emit("Evento de prueba");
            await Task.CompletedTask;
        }

        public Task ShowTextOnServer()
        {
            Console.WriteLine("Mostrando texto");
            return Task.CompletedTask;
        }
    }
}
