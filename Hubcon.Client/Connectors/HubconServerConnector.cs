using Castle.DynamicProxy;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Client.Connectors
{
    /// <summary>
    /// The ServerHubConnector allows a client to connect itself to a ServerHub and control its methods given its URL and
    /// the server's interface.
    /// </summary>
    /// <typeparam name="TIServerHubController"></typeparam>
    //public class HubconServerConnector<TICommunicationHandler> : IServerConnector, IHubconServerConnector<TICommunicationHandler>
    //    where TICommunicationHandler : ICommunicationHandler
    //{
    //    private IControllerContract? _client = null!;
    //    private readonly IContractInterceptor Interceptor;
    //    private readonly IProxyRegistry proxyRegistry;

    //    public ICommunicationHandler Connection { get => Interceptor.CommunicationHandler; }

    //    private IServiceProvider _serviceProvider { get; }

    //    public HubconServerConnector(
    //        IContractInterceptor interceptor,
    //        IProxyRegistry proxyRegistry,
    //        IServiceProvider serviceProvider) : base()
    //    {
    //        Interceptor = interceptor;
    //        this.proxyRegistry = proxyRegistry;
    //        _serviceProvider = serviceProvider;
    //    }

    //    public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract
    //    {
    //        if (_client != null)
    //            return (TICommunicationContract)_client;

    //        var proxyType = proxyRegistry.TryGetProxy<TICommunicationContract>();

    //        _client = (TICommunicationContract)_serviceProvider.GetService(proxyType, new[]
    //        {
    //            new TypedParameter(typeof(AsyncInterceptorBase), Interceptor)
    //        });

    //        return (TICommunicationContract)_client;
    //    }
    //}
}
