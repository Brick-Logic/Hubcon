using Hubcon;
using Hubcon.Shared.Abstractions.Attributes;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HubconTestDomain
{
    public interface IServerHubContract : IControllerContract
    {
        Task<int> GetTemperatureFromServer();
        IAsyncEnumerable<string> GetMessages(int count);
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }

    public class TestInputClass
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
    }

    [WebSocketTransport]
    [RateLimit(1)]
    public interface IUserContract : IControllerContract
    {
        Task<int> GetTemperatureFromServer(string test, CancellationToken cancellationToken = default);

        [HttpTransport]
        Task<HubconResponse<TestInputClass>> GetTemperatureFromServerWithInput(TestInputClass input, CancellationToken cancellationToken = default);

        Task<bool> GetTemperatureFromServerBlocking(CancellationToken cancellationToken = default);

        [ParseSseMessage("data: ")]
        [ParseSseMessage("event: ")]
        [ParseEndSseMessage("[DONE]")]
        IAsyncEnumerable<string> GetMessages(int count);

        Task ShowTextOnServer();
        Task<IEnumerable<bool>> GetBooleans();
        Task<MyTestClass> GetObject();

        [HttpTransport]
        Task CreateUser(CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> GetMessages2(CancellationToken cancellationToken = default);
        Task IngestMessages(IAsyncEnumerable<string> source, int? count, CancellationToken cancellationToken = default);
        Task<string> IngestMessages(IAsyncEnumerable<string> source, IAsyncEnumerable<string> source2, IAsyncEnumerable<string> source3, IAsyncEnumerable<string> source4, IAsyncEnumerable<string> source5);
        Task IngestMessages2(IAsyncEnumerable<string> source, IAsyncEnumerable<string> source2, IAsyncEnumerable<string> source3, IAsyncEnumerable<string> source4, IAsyncEnumerable<string> source5);
        IAsyncEnumerable<string> GetMessages(CancellationToken cancellationToken);
    }

    public class CreateUserCommandResponse
    {
        public bool Success { get; set; }
    }

    public class CreateUserCommand
    {
    }

    public class TestClass2
    {
        public TestClass2(string Propiedad)
        {
            this.Propiedad = Propiedad;
        }

        public string Propiedad { get; }
    }

    public class MyTestClass
    {
        public MyTestClass(string Propiedad, TestClass2 Myclass)
        {
            this.Propiedad = Propiedad;
            this.Myclass = Myclass;
        }

        public string Propiedad { get; }
        public TestClass2 Myclass { get; }
    }
}