using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IStreamSession : IDisposable
    {
        BaseMessage Payload { get; }

        void AddCancellation(Action callback, CancellationToken cancellationToken);
        void Next(JsonElement streamDataData);
        void TryComplete();
    }

    public interface IStreamSession<T> : IStreamSession
    {
        BaseMessage Payload { get; }

        IObservable<T> GetObservable();
    }
}
