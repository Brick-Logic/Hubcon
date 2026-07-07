using System.Reflection;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;

namespace Hubcon.Server.Core.EndpointManagement;

public sealed class ParameterWrapper : IParameterWrapper
{
    private readonly Func<IReadOnlyDictionary<string, object>, object?> _wrapperDelegate;

    public ParameterWrapper(string contractName, MethodInfo methodInfo, IEndpointManager endpointManager)
    {
        _wrapperDelegate = endpointManager.GetWrapperDelegate(contractName, methodInfo.GetMethodSignature()) 
                              ?? throw new HubconGenericException($"Could not find a parameter wrapper for the '{methodInfo.Name}' endpoint in '{methodInfo.DeclaringType}' controller. This error could be caused by an error while executing the source generators.");
    }
    
    public object GetWrapped(IReadOnlyDictionary<string, object> parameters)
    {
        return _wrapperDelegate.Invoke(parameters)!;
    }
}