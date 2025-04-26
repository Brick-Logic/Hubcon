using Hubcon.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Models
{
    public interface IHubconGraphQLClient
    {
        Task<BaseMethodResponse> SendRequestAsync(MethodInvokeRequest request, MethodInfo methodInfo, string resolver);
        IAsyncEnumerable<JsonElement> SubscribeToMessages(MethodInvokeRequest request, MethodInfo methodInfo, string resolver);
        IAsyncEnumerable<JsonElement> SubscribeUsingSSE(MethodInvokeRequest request, MethodInfo methodInfo, string resolver);
    }
}
