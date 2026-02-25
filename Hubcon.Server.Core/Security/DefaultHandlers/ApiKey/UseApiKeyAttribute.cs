using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Core.Security.DefaultHandlers.Jwt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Security.DefaultHandlers.ApiKey
{
    public sealed class UseApiKeyAttribute(string key, bool overrideAuthorization = true) : UseAuthAttribute<ApiKeyHandler>
    {
        public string Key { get; } = key;
        public bool ShouldOverrideAuthorization { get; set; } = overrideAuthorization;
    }
}
