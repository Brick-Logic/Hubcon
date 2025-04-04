using Hubcon.Core.Controllers;
using Hubcon.Core.Handlers;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;
using System.Threading.Channels;

namespace Hubcon.Core.Interfaces
{
    public interface IHubconController
    {
        IHubconControllerManager HubconController { get; }
        Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info);
        Task HandleMethodVoid(MethodInvokeRequest info);
    }

    public interface IHubconServerController : IHubconController
    {
        Task ReceiveStream(string code, ChannelReader<object> reader);
        IAsyncEnumerable<object> HandleMethodStream(MethodInvokeRequest info);

    }

    public interface IHubconTargetedClientController : IHubconController
    {
        Task StartStream(string methodCode, MethodInvokeRequest info);
    }
}
