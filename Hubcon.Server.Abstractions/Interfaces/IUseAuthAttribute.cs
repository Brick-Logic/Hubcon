using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Defines the core contract for security attributes in the Hubcon framework.
    /// This interface identifies the specific <see cref="IAuthHandler"/> required 
    /// to validate a request.
    /// </summary>
    public interface IUseAuthAttribute
    {
        /// <summary>
        /// Gets the type of the authentication handler associated with this attribute.
        /// <remarks>
        /// The specified type must implement <see cref="IAuthHandler"/> and is 
        /// typically resolved via Dependency Injection during the request lifecycle.
        /// </remarks>
        /// </summary>
        Type HandlerType { get; }
    }
}