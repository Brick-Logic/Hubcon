using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Models
{
    /// <summary>
    /// Model for Hubcon WebSocket messages.
    /// </summary>
    /// <param name="Bytes">Compiled bytes to send through the WebSocket channel.</param>
    /// <param name="CancellationToken">The origin operation's cancellation token.</param>
    public record ByteMessage(byte[] Bytes, CancellationToken CancellationToken);
}