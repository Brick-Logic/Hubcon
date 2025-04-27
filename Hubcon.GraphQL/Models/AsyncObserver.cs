using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Models
{
    public class AsyncObserver<T> : IObserver<T>
    {
        private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();
        private TaskCompletionSource<bool> _completed = new TaskCompletionSource<bool>();

        public async Task WriteToChannelAsync(T item)
        {
            await _channel.Writer.WriteAsync(item);
        }

        public IAsyncEnumerable<T> GetAsyncEnumerable()
        {
            return ReadAsync();
        }

        private async IAsyncEnumerable<T> ReadAsync()
        {
            await foreach (var item in _channel.Reader.ReadAllAsync())
            {
                yield return item;
            }
        }

        public void OnCompleted()
        {
            _channel.Writer.Complete();  // Cerramos el canal para que el ReadAsync termine.
            _completed.SetResult(true);   // Indicamos que la transmisión terminó.
        }

        public void OnError(Exception error)
        {
            // Manejar el error si es necesario
            _channel.Writer.Complete(error); // También cerramos el canal en caso de error.
            _completed.SetException(error);
        }

        public void OnNext(T value)
        {
            // Enviar el valor al canal
            WriteToChannelAsync(value).ConfigureAwait(false);
        }

        public Task WaitUntilCompleted()
        {
            return _completed.Task;
        }
    }
}
