using Hubcon;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace ExampleMicroservicesDomain
{
    [HttpTransport]
    public interface IExampleMicroservice3Contract : IControllerContract
    {
        public Task ProcessMessage(string message);
    }
}