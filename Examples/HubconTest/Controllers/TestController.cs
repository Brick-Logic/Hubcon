using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Attributes;
using HubconTestDomain;

namespace HubconTest.Controllers
{
    public class TestController : ITestContract
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
            Console.WriteLine(message);
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
            Console.WriteLine("Mostrando texto");
            return Task.CompletedTask;
        }
    }
}
