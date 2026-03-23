using System.Text.Json;
#pragma warning disable CS1591

namespace Hubcon.Experimental
{
    public interface IRpcStreamDispatcher
    {
        Task<IAsyncEnumerable<object>> DispatchStreamAsync(string target, JsonElement[] args, CancellationToken token);
    }
}