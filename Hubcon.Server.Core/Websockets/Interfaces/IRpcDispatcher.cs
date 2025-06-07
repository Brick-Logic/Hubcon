using System.Text.Json;

namespace Hubcon.Server.Core.Websockets.Interfaces
{
    public interface IRpcDispatcher
    {
        Task<object?> DispatchAsync(string target, JsonElement[] args);
    }
}