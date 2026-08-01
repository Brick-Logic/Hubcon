using System.Collections.Concurrent;
using System.Net;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Core.Tools;

namespace Hubcon.Server.Core.RateLimiting;

public sealed class ConnectionLimiter : IConnectionLimiter
{
    private readonly IInternalServerOptions _options;
    
    private readonly ConcurrentDictionary<HubconTransportAttribute, AtomicCounter> _globalCounters;
    
    private readonly ConcurrentDictionary<HubconTransportAttribute, ConcurrentDictionary<IPAddress, AtomicCounter>> _ipCounters;

    public ConnectionLimiter(IInternalServerOptions options)
    {
        _options = options;
        _globalCounters = new ConcurrentDictionary<HubconTransportAttribute, AtomicCounter>();
        _ipCounters = new ConcurrentDictionary<HubconTransportAttribute, ConcurrentDictionary<IPAddress, AtomicCounter>>();
    }

    public bool TryAcquire(IPAddress ipAddress, HubconTransportAttribute transport)
    {
        var settings = _options.GetTransportSettings(transport);

        int maxGlobal = settings.MaxConnections;
        int maxPerIp = settings.MaxConnectionsPerIp;

        if (maxGlobal <= 0 && maxPerIp <= 0)
            return true;

        var globalCounter = maxGlobal > 0 
            ? _globalCounters.GetOrAdd(transport, static (_, limit) => new AtomicCounter(limit), maxGlobal)
            : null;

        if (globalCounter != null && !globalCounter.TryIncrement())
        {
            return false; // Límite global alcanzado
        }

        if (maxPerIp <= 0)
            return true;

        var transportIpMap = _ipCounters.GetOrAdd(
            transport, 
            static _ => new ConcurrentDictionary<IPAddress, AtomicCounter>());

        var ipCounter = transportIpMap.GetOrAdd(
            ipAddress, 
            static (_, limit) => new AtomicCounter(limit), 
            maxPerIp);

        if (!ipCounter.TryIncrement())
        {
            globalCounter?.Decrement();
            return false;
        }

        return true;
    }

    public void Release(IPAddress ipAddress, HubconTransportAttribute transport)
    {
        var settings = _options.GetTransportSettings(transport);

        if (settings.MaxConnections > 0 && _globalCounters.TryGetValue(transport, out var globalCounter))
        {
            globalCounter.Decrement();
        }

        if (settings.MaxConnectionsPerIp > 0 && 
            _ipCounters.TryGetValue(transport, out var transportIpMap) &&
            transportIpMap.TryGetValue(ipAddress, out var ipCounter))
        {
            ipCounter.Decrement();
        }
    }
}