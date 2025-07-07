using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using HubconTestDomain;

namespace HubconTest.ContractHandlers
{
    public class UserContractHandler(ILogger<UserContractHandler> logger) : IUserContract
    {
        public ISubscription<int?>? OnUserCreated { get; }
        public ISubscription<int?>? OnUserCreated2 { get; }
        public ISubscription<int?>? OnUserCreated3 { get; }
        public ISubscription<int?>? OnUserCreated4 { get; }

        public Task CreateUser()
        {
            OnUserCreated?.Emit(1);
            OnUserCreated2?.Emit(2);
            OnUserCreated3?.Emit(3);
            OnUserCreated4?.Emit(4);
            //return Task.FromResult(new CreateUserCommandResponse { Success = false });
            return Task.CompletedTask;

            //var result = mediator.Send(command);

            //if (result.IsSuccess)
            //{
            //    return Task.FromResult(result);
            //}
            //else
            //{
            //}
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

        [StreamingSettings(ThrottleDelayMilliseconds:100)]
        public async IAsyncEnumerable<string> GetMessages2()
        {
            while(true)
            {
                yield return "hola2";
            }
        }

        public async Task PrintMessage(string message)
        {
            logger.LogInformation(message);
            await Task.CompletedTask;
        }

        dynamic mediator = null!;
        //[Authorize(Roles = ["Admin"])]


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


        public Task<MyTestClass> GetObject()
        {
            return Task.FromResult(new MyTestClass("hola", new TestClass2("propiedad")));
        }

        public async Task<IEnumerable<bool>> GetBooleans()
        {
            return Enumerable.Range(0, 5).Select(x => true);
        }
    }
}