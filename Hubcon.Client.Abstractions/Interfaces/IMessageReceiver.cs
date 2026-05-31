using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IMessageReceiver
    {
        IMessageRouter Router { get; }
    }
}