using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Routing.Registries
{
    public class PermissionRegistry : IPermissionRegistry
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public bool TryGet(string tokenId, string permission, out bool isAllowed)
        {
            return _cache.TryGetValue((tokenId, permission), out isAllowed);
        }

        public void Set(string tokenId, string permission, bool isAllowed, TimeSpan ttl)
        {
            _cache.Set((tokenId, permission), isAllowed, ttl);
        }
    }
}
