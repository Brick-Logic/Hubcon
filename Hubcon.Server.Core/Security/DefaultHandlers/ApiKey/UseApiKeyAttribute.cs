using Hubcon.Server.Abstractions.CustomAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Specifies that a specific API Key is required to access the decorated contract or operation.
    /// This attribute triggers the <see cref="ApiKeyHandler"/> during the request lifecycle.
    /// </summary>
    /// <param name="key">The expected API Key string (shared secret).</param>
    /// <param name="overrideAuthorization">
    /// If <see langword="true"/>, this API Key requirement takes precedence over other 
    /// global or contract-level authorization policies.
    /// </param>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseApiKeyAttribute(string key, bool overrideAuthorization = true) : UseAuthAttribute<ApiKeyHandler>
    {
        /// <summary>
        /// Gets the shared secret key required for successful authentication.
        /// </summary>
        public string Key { get; } = key;

        /// <summary>
        /// Gets or sets a value indicating whether this attribute should bypass or 
        /// supplement existing authorization requirements.
        /// </summary>
        public bool ShouldOverrideAuthorization { get; set; } = overrideAuthorization;
    }
}
