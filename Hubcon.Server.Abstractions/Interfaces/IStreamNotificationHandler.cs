using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Threading.Channels;
#pragma warning disable CS1591

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IStreamNotificationHandler
    {
        Task<IHubconResponse> NotifyStream(string code, ChannelReader<object> reader);
        Task<IAsyncEnumerable<T>> WaitStreamAsync<T>(string code);
    }
}