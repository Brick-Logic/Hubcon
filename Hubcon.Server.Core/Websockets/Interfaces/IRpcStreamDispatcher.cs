using System.Text.Json;

namespace Hubcon.Server.Core.Websockets.Interfaces
{
    public interface IRpcStreamDispatcher
    {
        Task<IAsyncEnumerable<object>> DispatchStreamAsync(string target, JsonElement[] args, CancellationToken token);
    }
}