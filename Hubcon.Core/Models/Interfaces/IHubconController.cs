using Hubcon.Core.Handlers;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;
using System.Threading.Channels;

namespace Hubcon.Core.Interfaces
{
    public interface IHubconController
    {
        ICommunicationHandler? CommunicationHandler { get; set; }
        MethodHandler? MethodHandler { get; set; }
        Task<MethodResponse> HandleTask(MethodInvokeRequest info);
        Task HandleVoid(MethodInvokeRequest info);
    }

    public interface IHubconServerController : IHubconController
    {
        Task ReceiveStream(string code, ChannelReader<object> reader);
    }

    public interface IHubconTargetedClientController : IHubconController
    {
        Task HandleStream(string methodCode, MethodInvokeRequest info);
    }
}
