using Hubcon;

namespace ExampleMicroservicesDomain
{
    [HttpTransport]
    public interface IExampleMicroservice1Contract : IControllerContract
    {
        public Task FinishMessage(string message);
        public Task ProcessMessage(string message);
    }
}
