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

        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task<IEnumerable<bool>> GetBooleans();
        Task IngestMessages(
            IAsyncEnumerable<string> source, 
            IAsyncEnumerable<string> source2, 
            IAsyncEnumerable<string> source3, 
            IAsyncEnumerable<string> source4, 
            IAsyncEnumerable<string> source5);
        Task<MyTestClass> GetObject();

        Task CreateUser();
        IAsyncEnumerable<string> GetMessages2();
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







