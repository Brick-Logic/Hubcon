using System.Text.Json;

namespace Hubcon.Websockets.Server.Interfaces
{
    public interface IRpcDispatcher
    {
        Task<object?> DispatchAsync(string target, JsonElement[] args);
    }
}