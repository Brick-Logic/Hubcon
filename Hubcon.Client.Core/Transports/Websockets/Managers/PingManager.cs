using System;
using System.Threading.Tasks;
using Hubcon.Client.Abstractions.Interfaces;

namespace Hubcon.Client.Core.Transports.Websockets.Managers
{
    /// <summary>
    /// Manages ping/pong messages for the given websocket connection.
    /// </summary>
    public sealed class PingManager : IPingManager, IAsyncDisposable
    {
        
        /// <summary>
        /// Disposes this object and its resources.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}