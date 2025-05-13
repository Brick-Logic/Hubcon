using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;

namespace Hubcon.GraphQL.TypeResolvers
{
    public class MethodInvokeRequestResolver
    {
        public IOperationRequest ResolveMethodInvokeRequest(MethodInvokeRequest request)
        {
            return request;
        }
    }
}
