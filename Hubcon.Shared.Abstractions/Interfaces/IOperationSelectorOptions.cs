using System.Collections.Concurrent;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a global registry for all resolved operation options within the Hubcon framework.
    /// Acts as a top-level cache for <see cref="IOperationOptions"/>, indexed by their unique 
    /// method signatures or identifiers.
    /// </summary>
    public interface IGlobalOperationOptions
    {
        /// <summary>
        /// Gets a thread-safe dictionary containing the final, resolved options for every 
        /// registered operation in the system.
        /// <remarks>
        /// The key is typically the unique method signature string (e.g., "IMyService.GetDataAsync"),
        /// allowing for O(1) lookup during the request dispatch lifecycle.
        /// </remarks>
        /// </summary>
        ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; }
    }
}
