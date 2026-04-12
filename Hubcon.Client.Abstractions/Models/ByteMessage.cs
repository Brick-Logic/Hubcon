using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Models
{
    /// <summary>
    /// Model for Hubcon WebSocket messages.
    /// </summary>
    public class ByteMessage
    {
        /// <summary>
        /// Compiled bytes to send through the WebSocket channel.
        /// </summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// The connection ID used for this message.
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// The origin operation's cancellation token.
        /// </summary>
        public CancellationToken CancellationToken { get; }


        /// <param name="Bytes">Compiled bytes to send through the WebSocket channel.</param>
        /// <param name="connectionId">The connection ID used for this message.</param>
        /// <param name="CancellationToken">The origin operation's cancellation token.</param>
        public ByteMessage(byte[] Bytes, string connectionId, CancellationToken CancellationToken)
        {
            this.Bytes = Bytes;
            ConnectionId = connectionId;
            this.CancellationToken = CancellationToken;
        }
    }
}