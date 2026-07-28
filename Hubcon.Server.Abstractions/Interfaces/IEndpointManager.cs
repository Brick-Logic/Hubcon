using System.Reflection;

namespace Hubcon.Server.Abstractions.Interfaces;

public interface IEndpointManager
{
    public IEndpointInvoker? GetInvoker(string contractName, string signature);

    public Func<IReadOnlyDictionary<string, object>, object>? GetWrapperDelegate(string contractName, string methodSignature);
    public Type? GetWrapperType(string contractName, string methodSignature);
}