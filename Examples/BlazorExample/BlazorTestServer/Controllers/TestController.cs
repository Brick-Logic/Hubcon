using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;

namespace BlazorTestServer.Controllers
{
    public class TestController(ILogger<TestController> logger) : IUserContract
    {
        [AllowAnonymous]
        public ISubscription<int?>? OnUserCreated { get; }

        public ISubscription<int?>? OnUserCreated2 { get; }

        public ISubscription<int?>? OnUserCreated3 { get; }

        public ISubscription<int?>? OnUserCreated4 { get; }

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

        public async Task IngestMessages(
            IAsyncEnumerable<string> source,
            IAsyncEnumerable<string> source2,
            IAsyncEnumerable<string> source3,
            IAsyncEnumerable<string> source4,
            IAsyncEnumerable<string> source5)
        {
            Task TaskRunner<T>(IAsyncEnumerable<T> source, string name)
            {
                return Task.Run(async () =>
                {
                    await foreach (var item in source)
                    {
                        logger.LogInformation($"source1: {item}");
                    }
                    logger.LogInformation($"[{name}] Stream terminado.");
                });
            }

            List<Task> sources =
            [
                TaskRunner(source, nameof(source)),
                TaskRunner(source2, nameof(source2)),
                TaskRunner(source3, nameof(source3)),
                TaskRunner(source4, nameof(source4)),
                TaskRunner(source5, nameof(source5)),
            ];

            await Task.WhenAll(sources);
            logger.LogInformation("Ingest terminado exitosamente");
        }

        public async Task IngestMessages(IAsyncEnumerable<string> source)
        {
            Task TaskRunner<T>(IAsyncEnumerable<T> source, string name)
            {
                return Task.Run(async () =>
                {
                    await foreach (var item in source)
                    {
                        logger.LogInformation($"source1: {item}");
                    }
                    logger.LogInformation($"[{name}] Stream terminado.");
                });
            }

            List<Task> sources = [TaskRunner(source, nameof(source))];

            await Task.WhenAll(sources);
            logger.LogInformation("Ingest terminado exitosamente");
        }

        public Task<IEnumerable<bool>> GetBooleans()
        {
            return Task.FromResult(Enumerable.Range(0, 6).Select(x => true));
        }

        public Task<MyTestClass> GetObject()
        {
            return Task.FromResult(new MyTestClass("hola", new TestClass2("propiedad")));
        }

        public IAsyncEnumerable<string> GetMessages2()
        {
            throw new NotImplementedException();
        }
    }
}
