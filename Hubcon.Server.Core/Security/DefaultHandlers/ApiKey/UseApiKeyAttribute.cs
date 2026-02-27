using Hubcon.Server.Abstractions.CustomAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public sealed class UseApiKeyAttribute(string key, bool overrideAuthorization = true) : UseAuthAttribute<ApiKeyHandler>
    {
        public string Key { get; } = key;
        public bool ShouldOverrideAuthorization { get; set; } = overrideAuthorization;
    }
}
