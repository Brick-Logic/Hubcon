using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Models;
using System.Text.Json;

namespace Hubcon.Shared.Core.Websockets.Events
{
    public abstract class BaseObservable
    {
        protected readonly IUnsubscriber? _client;
        public IRequest? RequestData { get; }

        protected BaseObservable(IUnsubscriber? client, IRequest? request)
        {
            _client = client;
            RequestData = request;
        }

        public abstract void OnNextElement(JsonElement value);
        public abstract void OnNextObject(object value);
        public abstract void OnError(Exception ex);
        public abstract void OnCompleted();
    }

    public class GenericObservable<TMessage> : BaseObservable, IObservable<TMessage>
    {
        private readonly List<IObserver<TMessage>> _observers = new();
        private readonly object _observersLock = new();
        private readonly Type _dataType = typeof(TMessage);
        private readonly IDynamicConverter converter;

        public Type DataType => _dataType;

        public GenericObservable(
            IUnsubscriber client, 
            string id, 
            JsonElement request, 
            RequestType type, 
            IDynamicConverter converter) : base(client, new RequestData(id, request, type))
        {
            this.converter = converter;
        }

        public GenericObservable(IDynamicConverter converter) : base(null, null)
        {
            this.converter = converter;
        }

        public IDisposable Subscribe(IObserver<TMessage> observer)
        {
            lock (_observersLock)
            {
                _observers.Add(observer);
            }

            return new Unsubscriber(this, observer);
        }

        public override void OnNextElement(JsonElement value)
        {
            var data = converter.DeserializeJsonElement<TMessage>(value);
            OnNext(data!);
        }

        public override void OnNextObject(object value)
        {
            OnNext((TMessage)value);
        }

        public void OnNext(TMessage value)
        {
            IObserver<TMessage>[] snapshot;
            lock (_observersLock)
            {
                snapshot = _observers.ToArray();
            }

            foreach (var o in snapshot)
            {
                try { o.OnNext(value); } catch { /* Ignorar errores de observers */ }
            }
        }

        public override void OnError(Exception ex)
        {
            IObserver<TMessage>[] snapshot;
            lock (_observersLock)
            {
                snapshot = _observers.ToArray();
                _observers.Clear();
            }

            foreach (var o in snapshot)
            {
                try { o.OnError(ex); } catch { /* Ignorar errores */ }
            }
        }

        public override void OnCompleted()
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnCompleted();
                UnsubscribeObserver(observer);
            }

            _observers.Clear();
        }

        private void UnsubscribeObserver(IObserver<TMessage> observer)
        {
            if (_client == null || RequestData == null)
                return;

            lock (_observersLock)
            {
                _observers.Remove(observer);
                if (_observers.Count == 0)
                {
                    // No quedan observers, remover la subscripción completa

                    _client.Unsubscribe(RequestData);
                }
            }
        }

        private class Unsubscriber : IDisposable
        {
            private readonly GenericObservable<TMessage> _parent;
            private readonly IObserver<TMessage> _observer;
            private bool _disposed = false;

            public Unsubscriber(GenericObservable<TMessage> parent, IObserver<TMessage> observer)
            {
                _parent = parent;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _parent.UnsubscribeObserver(_observer);
            }
        }
    }
}
