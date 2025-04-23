using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Dummy
{
    public class DummyCommunicationHandler : ICommunicationHandler
    {
        public Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
