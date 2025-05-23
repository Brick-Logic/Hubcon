using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IHubconClientProvider
    {
        TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract;
    }
}
