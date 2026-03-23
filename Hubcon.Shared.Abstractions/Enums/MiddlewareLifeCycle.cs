using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Specifies the lifetime of a middleware within the Hubcon framework.
    /// Determines how middleware instances are created and shared across requests.
    /// </summary>
    public enum MiddlewareLifeCycle
    {
        /// <summary>
        /// A single instance of the middleware is created and shared throughout the entire lifetime of the server.
        /// </summary>
        Singleton,

        /// <summary>
        /// A new instance of the middleware is created once per request (or connection scope).
        /// </summary>
        Scoped,

        /// <summary>
        /// A new instance of the middleware is created every time it is requested from the service provider.
        /// </summary>
        Transient
    }
}
