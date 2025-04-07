using Hubcon.Core.Converters;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Core.Handlers
{
    public static class StreamHandler
    {
        private static Dictionary<string, IOnStreamReceived> StreamWaitList = new();

        public static async Task NotifyStream(string code, ChannelReader<object> reader)
        {
            while (await reader.WaitToReadAsync())
            {
                if (reader.CanPeek)
                {
                    break;
                }
            }

            if (StreamWaitList.TryGetValue(code, out var value))
            {
                value.GetCurrentEvent()?.DynamicInvoke(reader);
            }
        }

        public static async Task<IAsyncEnumerable<T>> WaitStreamAsync<T>(string code)
        {
            static async IAsyncEnumerable<TOut> ToAsyncEnumerable<TOut>(ChannelReader<object> reader)
            {
                await foreach (object item in reader.ReadAllAsync())
                {
                    yield return DynamicConverter.DeserializeData<TOut>(item)!;
                }
            }

            var tcs = new TaskCompletionSource<IAsyncEnumerable<T>>();
            var eventHolder = new OnStreamReceived();

            Func<ChannelReader<object>, Task> eventHandler = null!;

            eventHandler = (enumerable) =>
            {
                // Cuando el evento se dispara, resolvemos la tarea
                tcs.SetResult(ToAsyncEnumerable<T>(enumerable));
                // Es importante desuscribirse después de que el evento se ha manejado
                eventHolder.OnStreamReceivedEvent -= eventHandler;

                return Task.CompletedTask;
            };

            StreamWaitList.Add(code, eventHolder);

            eventHolder.OnStreamReceivedEvent += eventHandler;

            // Esperar a que el evento se dispare
            await tcs.Task;

            return tcs.Task.Result;
        }
    }
}
