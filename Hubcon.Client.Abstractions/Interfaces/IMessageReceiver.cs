using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IMessageReceiver
    {
        /// <summary>
        /// An event that's raised when the reception loop produces an error.
        /// </summary>
        public event EventHandler<Exception>? OnError;
        
        /// <summary>
        /// An event that's raised when the websocket is disconnected for any reason.
        /// </summary>
        public event Action? OnDisconnected;
        
        /// <summary>
        /// An event that's raised when the websocket receives a Close message.
        /// </summary>
        public event Action? OnCloseReceived;
        
        IMessageRouter Router { get; }

        void Start();
    }
}