using System.Reflection;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;

namespace Hubcon.Server.Core.EndpointManagement;

public sealed class ParameterWrapper : IParameterWrapper
{
    private readonly Func<IReadOnlyDictionary<string, object>, object?> _wrapperDelegate;

    public ParameterWrapper(Type controllerType, Type contractType, MethodInfo methodInfo)
    {
        _wrapperDelegate = EndpointManager.GetWrapperDelegate(controllerType, contractType, methodInfo) 
                              ?? throw new HubconGenericException($"Could not find a parameter wrapper for the '{methodInfo.Name}' endpoint in '{methodInfo.DeclaringType}' controller. This error could be caused by an error while executing the source generators.");
    }
    
    public IWrapper GetWrapped(IReadOnlyDictionary<string, object> parameters)
    {
        return (_wrapperDelegate.Invoke(parameters)! as IWrapper)!;
    }
}