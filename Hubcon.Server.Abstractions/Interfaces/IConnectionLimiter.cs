using System.Net;

namespace Hubcon;

/// <summary>
/// Defines a high-performance, thread-safe connection limiter designed to enforce global 
/// and per-IP connection limits across different transport layers.
/// </summary>
public interface IConnectionLimiter
{
    /// <summary>
    /// Attempts to acquire a connection slot for a given IP address and transport layer.
    /// </summary>
    /// <param name="ipAddress">The remote <see cref="IPAddress"/> requesting a connection.</param>
    /// <param name="transport">The <see cref="HubconTransportAttribute"/> identifying the transport type.</param>
    /// <returns>
    /// <see langword="true"/> if the connection slot was successfully acquired within configured limits; 
    /// otherwise, <see langword="false"/> if global or per-IP connection thresholds have been exceeded.
    /// </returns>
    /// <remarks>
    /// This method is non-blocking and atomic. If the acquisition fails (returns <see langword="false"/>), 
    /// the caller must immediately drop or reject the incoming socket connection.
    /// </remarks>
    public bool TryAcquire(IPAddress ipAddress, HubconTransportAttribute transport);

    /// <summary>
    /// Releases a previously acquired connection slot, decrementing active counters.
    /// </summary>
    /// <param name="ipAddress">The remote <see cref="IPAddress"/> of the disconnected client.</param>
    /// <param name="transport">The <see cref="HubconTransportAttribute"/> identifying the transport type.</param>
    /// <remarks>
    /// Must be called once when a socket or transport session terminates (e.g., in a <c>finally</c> block 
    /// or connection teardown pipeline) to prevent resource leaks in connection counters.
    /// </remarks>
    public void Release(IPAddress ipAddress, HubconTransportAttribute transport);
}