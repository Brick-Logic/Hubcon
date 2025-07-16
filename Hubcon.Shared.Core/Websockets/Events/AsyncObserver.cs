using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Shared.Core.Websockets.Events
{
    public class AsyncObserver<T>(BoundedChannelOptions? options = null) : IObserver<T>
    {
        private readonly Channel<T?> _channel = Channel.CreateBounded<T?>(options ?? new BoundedChannelOptions(5000)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        private TaskCompletionSource<bool> _completed = new TaskCompletionSource<bool>();

        public async Task<bool> WriteToChannelAsync(T? item)
        {
            try
            {
                var toWrite = item is JsonElement element
                    ? GetTType(element)
                    : item;

                await _channel.Writer.WriteAsync(toWrite!);
                return true;
            }
            catch (ChannelClosedException ex)
            {
                // El canal ya fue completado/cerrado
                return false;
            }
            catch(Exception ex)
            {
                // Manejar otras excepciones según sea necesario
                OnError(ex);
                return false;
            }
        }

        public T GetTType(JsonElement item)
        {
            return (T)(object)item.Clone();
        }

        public IAsyncEnumerable<T?> GetAsyncEnumerable(CancellationToken cancellationToken)
        {
            try
            {
                return ReadAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                OnError(ex);
                throw;
            }
        }

        private async IAsyncEnumerable<T?> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {       
            await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }

        public void OnCompleted()
        {
            _channel.Writer.Complete();
            _completed.SetResult(true);
        }

        public void OnError(Exception error)
        {
            _channel.Writer.Complete(error);
            _completed.SetException(error);
        }

        public async void OnNext(T value)
        {
            // Enviar el valor al canal
            var result = await WriteToChannelAsync(value);
        }

        public Task WaitUntilCompleted()
        {
            return _completed.Task;
        }
    }
}