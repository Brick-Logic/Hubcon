using Hubcon.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Models
{
    public interface IHubconGraphQLClient
    {
        Task<BaseMethodResponse> SendRequestAsync(MethodInvokeRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetStream(MethodInvokeRequest request, string resolver, CancellationToken cancellationToken = default);
    }
}
