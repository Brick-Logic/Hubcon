using System.Collections.Concurrent;
using System.Reflection;
using Hubcon.Server.Abstractions.Interfaces;

namespace Hubcon;

public sealed class EndpointManager : IEndpointManager
{
    private static Func<string, string, IEndpointInvoker>? _invokerProvider = null;
    private static readonly ConcurrentDictionary<(string, string, Func<string, string, IEndpointInvoker>), IEndpointInvoker> _invokerCache = new();
    
    private static Func<string, string, Func<IReadOnlyDictionary<string, object>, object>?>? _parameterWrapperProvider = null;
    private static readonly ConcurrentDictionary<(string, string, Func<string, string, Func<IReadOnlyDictionary<string, object>, object>?>), Func<IReadOnlyDictionary<string, object>, object>?> _parameterWrapperCache = new();
    
    private static Func<string, string, Type?>? _parameterWrapperTypeProvider = null;
    private static readonly ConcurrentDictionary<(string, string, Func<string, string, Type?>), Type?> _parameterWrapperTypeCache = new();
    
    public static void Setup(
        Func<string, string, IEndpointInvoker> invokerProvider,
        Func<string, string, Type?> parameterWrapperTypeProvider,
        Func<string, string, Func<IReadOnlyDictionary<string, object>, object>?> parameterWrapperProvider)
    {
        _invokerProvider ??= invokerProvider;
        _parameterWrapperTypeProvider ??= parameterWrapperTypeProvider;
        _parameterWrapperProvider ??= parameterWrapperProvider;
    }

    public IEndpointInvoker? GetInvoker(string contractName, string signature)
    {
        return _invokerProvider == null
            ? null 
            : _invokerCache.GetOrAdd((contractName, signature, _invokerProvider), x => x.Item3.Invoke(x.Item1, x.Item2));
    }

    public Func<IReadOnlyDictionary<string, object>, object>? GetWrapperDelegate(string contractName, string methodSignature)
    {
        return _parameterWrapperProvider == null
            ? null 
            : _parameterWrapperCache.GetOrAdd((contractName, methodSignature, _parameterWrapperProvider), x => x.Item3.Invoke(x.Item1, x.Item2));

    }
    
    public Type? GetWrapperType(string contractName, string methodSignature)
    {
        return _parameterWrapperTypeProvider == null
            ? null 
            : _parameterWrapperTypeCache.GetOrAdd((contractName, methodSignature, _parameterWrapperTypeProvider), x => x.Item3.Invoke(x.Item1, x.Item2));
    }
}