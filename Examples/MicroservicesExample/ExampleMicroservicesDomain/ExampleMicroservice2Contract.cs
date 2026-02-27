using Hubcon;

namespace ExampleMicroservicesDomain
{
    [HttpTransport]
    public interface IExampleMicroservice2Contract : IControllerContract
    {
        public Task ProcessMessage(string message);
    }
}
