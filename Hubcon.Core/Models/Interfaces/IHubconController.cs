using System.Threading.Channels;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IBaseHubconController
    {
        IHubconControllerManager HubconController { get; }
        Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info);
        Task HandleMethodVoid(MethodInvokeRequest info);
    }

    public interface IHubconServerController : IBaseHubconController
    {
        Task ReceiveStream(string code, ChannelReader<object> reader);
        IAsyncEnumerable<object> HandleMethodStream(MethodInvokeRequest info);

    }

    public interface IHubconClientController : IBaseHubconController
    {
        Task StartStream(string methodCode, MethodInvokeRequest info);
    }
}
