using System.Text.Json;
#pragma warning disable CS1591

namespace Hubcon.Experimental
{
    public interface IRpcDispatcher
    {
        Task<object?> DispatchAsync(string target, JsonElement[] args);
    }
}