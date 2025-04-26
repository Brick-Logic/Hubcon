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
    public class DummyCommunicationHandler : ICommunicationHandler
    {
        public Task CallAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
