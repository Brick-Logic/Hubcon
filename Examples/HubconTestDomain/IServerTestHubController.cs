using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace HubconTestDomain
{
    public interface IServerHubContract : IControllerContract
    {
        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }

    public interface IUserContract : IControllerContract
    {
        ISubscription<int?>? OnUserCreated { get; }
        ISubscription<int?>? OnUserCreated2 { get; }
        ISubscription<int?>? OnUserCreated3 { get; }
        ISubscription<int?>? OnUserCreated4 { get; }

        Task<int> GetTemperatureFromServer(CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task<IEnumerable<bool>> GetBooleans();
        Task<MyTestClass> GetObject();
        Task CreateUser(CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GetMessages2(CancellationToken cancellationToken = default);
        Task IngestMessages(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default);
        Task<string> IngestMessages(IAsyncEnumerable<string> source, IAsyncEnumerable<string> source2, IAsyncEnumerable<string> source3, IAsyncEnumerable<string> source4, IAsyncEnumerable<string> source5);
        Task IngestMessages2(IAsyncEnumerable<string> source, IAsyncEnumerable<string> source2, IAsyncEnumerable<string> source3, IAsyncEnumerable<string> source4, IAsyncEnumerable<string> source5);
    }

    public class CreateUserCommandResponse
    {
        public bool Success { get; set; }
    }

    public class CreateUserCommand
    {
    }

    public record class TestClass2(string Propiedad);
    public record class MyTestClass(string Propiedad, TestClass2 Myclass);
}







