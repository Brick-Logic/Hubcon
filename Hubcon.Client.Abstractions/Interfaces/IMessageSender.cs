using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IMessageSender
    {
        ValueTask DisposeAsync();
        ValueTask SendMessageAsync<T>(T message, CancellationToken cancellationToken = default) where T : BaseMessage;
    }
}