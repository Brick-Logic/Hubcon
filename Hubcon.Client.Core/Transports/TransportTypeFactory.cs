using Hubcon.Client.Core.Transports.HubconHttp;
using Hubcon.Client.Core.Transports.NonHubconHttp;
using Hubcon.Client.Core.Transports.Websockets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#pragma warning disable CS1591

namespace Hubcon.Client.Core.Transports
{
    public static class TransportTypeResolver
    {
        private static readonly Dictionary<Type, Type> _lookups = new()
        {
            { typeof(WebSocketTransport), typeof(WebSocketTransportClient) },
            { typeof(HttpTransport), typeof(HttpTransportClient) },
            { typeof(NonHubconHttpTransport), typeof(NonHubconHttpTransportClient) }
        };

        public static void RegisterMappings(Dictionary<Type, Type> mappings)
        {
            foreach (var kvp in mappings)
            {
                _lookups.TryAdd(kvp.Key, kvp.Value);
            }
        }

        public static int GetTransportsCount()
        {
            return _lookups.Count;
        }

        public static IReadOnlyDictionary<Type, Type> GetMappings()
        {
            return _lookups.ToImmutableDictionary();
        }

        public static Type? Resolve(Type marker)
        {
            if (marker == null)
                return null;

            if (_lookups.TryGetValue(marker, out var value))
            {
                return value;
            }

            return null;
        }
    }
}
