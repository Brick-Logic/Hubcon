using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace ExampleMicroservicesDomain
{
    public interface IExampleMicroservice1Contract : IControllerContract
    {
        public Task FinishMessage(string message);
        public Task ProcessMessage(string message);
    }
}
