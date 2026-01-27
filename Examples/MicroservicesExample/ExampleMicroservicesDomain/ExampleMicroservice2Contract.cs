using Hubcon;

namespace ExampleMicroservicesDomain
{
    public interface IExampleMicroservice2Contract : IControllerContract
    {
        public Task ProcessMessage(string message);
    }
}
