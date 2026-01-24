using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Threading.Channels;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IStreamNotificationHandler
    {
        Task<IHubconResponse> NotifyStream(string code, ChannelReader<object> reader);
        Task<IAsyncEnumerable<T>> WaitStreamAsync<T>(string code);
    }
}