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
        ISubscription<int>? OnUserCreated { get; }
        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task<IEnumerable<bool>> GetBooleans();
        Task CreateUser();
        Task IngestMessages(
            IAsyncEnumerable<string> source, 
            IAsyncEnumerable<string> source2, 
            IAsyncEnumerable<string> source3, 
            IAsyncEnumerable<string> source4, 
            IAsyncEnumerable<string> source5);
        Task<MyTestClass> GetObject();
    }

    public record class TestClass2(string Propiedad);
    public record class MyTestClass(string Propiedad, TestClass2 Myclass);
}







