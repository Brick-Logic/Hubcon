using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.TypeResolvers
{
    public class MethodInvokeRequestResolver
    {
        public IOperationRequest ResolveMethodInvokeRequest(IOperationRequest request)
        {
            return request;
        }
    }
}
