using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace ExampleMicroservicesDomain
{
    public interface IExampleMicroservice3Contract : IControllerContract
    {
        public Task ProcessMessage(string message);
    }
}