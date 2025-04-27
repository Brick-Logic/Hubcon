using Hubcon.Core.Connectors;
using Hubcon.Core.Models.Interfaces;
using Hubcon.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.GraphQL.Client
{
    public class HubconClientProvider
    {
        private readonly IServiceProvider serviceProvider;

        public HubconClientProvider(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public TICommunicationContract GetClient<TICommunicationContract>()
            where TICommunicationContract : IControllerContract

        {
            var connector = serviceProvider.GetRequiredService<HubconServerConnector<ICommunicationHandler>>();
            return connector.GetClient<TICommunicationContract>();
        }      
    }
}
