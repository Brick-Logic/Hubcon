using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.GraphQL.Client
{
    public class HubconClientProvider : IHubconClientProvider
    {
        private readonly IServiceProvider serviceProvider;

        public HubconClientProvider(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public TICommunicationContract GetClient<TICommunicationContract>()
            where TICommunicationContract : IControllerContract

        {
            var connector = serviceProvider.GetRequiredService<IHubconServerConnector<ICommunicationHandler>>();
            return connector.GetClient<TICommunicationContract>();
        }      
    }
}
