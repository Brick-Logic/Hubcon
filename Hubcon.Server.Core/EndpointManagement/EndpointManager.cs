using System.Collections.Concurrent;
using System.Reflection;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Microsoft.AspNetCore.Routing;

namespace Hubcon;

public static class EndpointManager
{
    private static Func<string, string, string, Delegate?>? _dummyDelegateProvider = null;
    private static readonly ConcurrentDictionary<(string, string, string, Func<string, string, string, Delegate?>), Delegate?> _dummyDelegateCache = new();
    
    private static Func<string, string, string, IEndpointInvoker>? _invokerProvider = null;
    private static readonly ConcurrentDictionary<(string, string, string, Func<string, string, string, IEndpointInvoker>), IEndpointInvoker> _invokerCache = new();
    
    private static Func<string, string, string, Func<IReadOnlyDictionary<string, object>, object>?>? _parameterWrapperProvider = null;
    private static readonly ConcurrentDictionary<(string, string, string, Func<string, string, string, Func<IReadOnlyDictionary<string, object>, object>?>), Func<IReadOnlyDictionary<string, object>, object>?> _parameterWrapperCache = new();
    
    private static Func<string, string, string, Type?>? _parameterWrapperTypeProvider = null;
    private static readonly ConcurrentDictionary<(string, string, string, Func<string, string, string, Type?>), Type?> _parameterWrapperTypeCache = new();
    
    public static void Setup(
        Func<string, string, string, Delegate?> httpEndpointMapper,
        Func<string, string, string, IEndpointInvoker> invokerProvider,
        Func<string, string, string, Type?> parameterWrapperTypeProvider,
        Func<string, string, string, Func<IReadOnlyDictionary<string, object>, object>?> parameterWrapperProvider)
    {
        _dummyDelegateProvider ??= httpEndpointMapper;
        _invokerProvider ??= invokerProvider;
        _parameterWrapperTypeProvider ??= parameterWrapperTypeProvider;
        _parameterWrapperProvider ??= parameterWrapperProvider;
    }

    public static IEndpointInvoker? GetInvoker(Type controllerType, Type contractType, MethodInfo method)
    {
        var item = _invokerProvider == null
            ? null 
            : _invokerCache.GetOrAdd((controllerType.Name, contractType.Name, method.GetMethodSignature(), _invokerProvider), x => x.Item4.Invoke(x.Item1, x.Item2, x.Item3));

        return item;
    }

    public static Func<IReadOnlyDictionary<string, object>, object>? GetWrapperDelegate(Type controllerType, Type contractType, MethodInfo method)
    {
        var item = _parameterWrapperProvider == null
            ? null 
            : _parameterWrapperCache.GetOrAdd((controllerType.Name, contractType.Name, method.GetMethodSignature(), _parameterWrapperProvider), x => x.Item4.Invoke(x.Item1, x.Item2, x.Item3));

        return item;
    }
    
    public static Type? GetWrapperType(Type controllerType, Type contractType, MethodInfo method)
    {
        return _parameterWrapperTypeProvider == null
            ? null 
            : _parameterWrapperTypeCache.GetOrAdd((controllerType.Name, contractType.Name, method.GetMethodSignature(), _parameterWrapperTypeProvider), x => x.Item4.Invoke(x.Item1, x.Item2, x.Item3));
    }
    
    public static Delegate? GetDummyEndpointDelegate(Type controllerType, Type contractType, MethodInfo method)
    {
        return _dummyDelegateProvider == null
            ? null 
            : _dummyDelegateCache.GetOrAdd((controllerType.Name, contractType.Name, method.GetMethodSignature(), _dummyDelegateProvider), x => x.Item4.Invoke(x.Item1, x.Item2, x.Item3));
    }
}