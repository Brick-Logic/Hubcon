using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Dummy
{
    public class DummyServerCommunicationHandler : IServerCommunicationHandler
    {
        public Task CallAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public List<IClientReference> GetAllClients()
        {
            return Array.Empty<IClientReference>().ToList();
        }

        public async Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return await Task.FromResult(new BaseMethodResponse(true));
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult<IAsyncEnumerable<T?>>(null!);
        }

        public IServerCommunicationHandler WithClientId(string clientId)
        {
            return this;
        }
    }
}
