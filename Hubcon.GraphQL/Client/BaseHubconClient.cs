using Hubcon.Core.Connectors;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using Hubcon.SignalR.Client;
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
