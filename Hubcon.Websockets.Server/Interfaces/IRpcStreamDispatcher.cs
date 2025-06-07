using System.Text.Json;

namespace Hubcon.Websockets.Server.Interfaces
{
    public interface IRpcStreamDispatcher
    {
        Task<IAsyncEnumerable<object>> DispatchStreamAsync(string target, JsonElement[] args, CancellationToken token);
    }
}