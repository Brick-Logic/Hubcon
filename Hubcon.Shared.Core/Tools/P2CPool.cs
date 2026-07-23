using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Shared.Core.Extensions;

namespace Hubcon.Shared.Core.Tools
{
    public class P2CPool<T> : IAsyncDisposable, IDisposable where T : class
    {
        private readonly Entry[] _items;
        private readonly Random _random = new();
        private int _isDisposed;

        private class Entry
        {
            public readonly T Instance;
            public int ActiveRequestsCount;

            public Entry(T instance)
            {
                Instance = instance;
            }
        }

        public P2CPool(IServiceProvider provider, Func<IServiceProvider, T> factory, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");

            Throw.IfNull(factory);
            Throw.IfNull(provider);

            _items = new Entry[count];
            for (var i = 0; i < count; i++)
            {
                var instance = factory(provider);
                Throw.IfNull(instance, $"Factory returned null for index {i}");
                _items[i] = new Entry(instance);
            }
        }

        public int Count => _items.Length;

        #region Execution Methods (P2C)

        /// <summary>
        /// Ejecuta una acción asíncrona sobre la mejor opción según P2C.
        /// </summary>
        public async Task ExecuteAsync<TState>(Func<T, TState, Task> action, TState state)
        {
            Throw.If(_isDisposed != 0, static () => new ObjectDisposedException(nameof(P2CPool<T>)));
            Throw.IfNull(action);

            var entry = SelectBestEntry();

            Interlocked.Increment(ref entry.ActiveRequestsCount);
            try
            {
                await action(entry.Instance, state).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref entry.ActiveRequestsCount);
            }
        }

        /// <summary>
        /// Ejecuta una función asíncrona con retorno sobre la mejor opción según P2C.
        /// </summary>
        public async Task<TResponse> ExecuteAsync<TState, TResponse>(Func<T, TState, Task<TResponse>> action,
            TState state)
        {
            Throw.If(_isDisposed != 0, static () => new ObjectDisposedException(nameof(P2CPool<T>)));
            Throw.IfNull(action);

            var entry = SelectBestEntry();

            Interlocked.Increment(ref entry.ActiveRequestsCount);
            try
            {
                return await action(entry.Instance, state).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref entry.ActiveRequestsCount);
            }
        }

        /// <summary>
        /// Permite filtrar o seleccionar una instancia específica mediante un discriminador, 
        /// ejecutando la acción sobre la mejor opción P2C entre las que cumplan la condición.
        /// </summary>
        public async Task<TResponse> ExecuteAsync<TState, TResponse>(
            Func<T, bool> discriminator,
            Func<T, TState, Task<TResponse>> action, TState state)
        {
            Throw.If(_isDisposed != 0, static () => new ObjectDisposedException(nameof(P2CPool<T>)));
            Throw.IfNull(discriminator);
            Throw.IfNull(action);

            var entry = SelectBestEntry(discriminator);

            Interlocked.Increment(ref entry.ActiveRequestsCount);
            try
            {
                return await action(entry.Instance, state).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref entry.ActiveRequestsCount);
            }
        }

        #endregion

        #region Selection Logic

        private Entry SelectBestEntry(Func<T, bool>? discriminator = null)
        {
            var len = _items.Length;

            if (len == 1)
            {
                if (discriminator != null && !discriminator(_items[0].Instance))
                    throw new InvalidOperationException("No instance matches the provided discriminator.");
                return _items[0];
            }

            var i1 = _random.Next(len);
            var i2 = _random.Next(len);

            if (i1 == i2)
                i2 = (i2 + 1) % len;

            var e1 = _items[i1];
            var e2 = _items[i2];

            var match1 = discriminator == null || discriminator(e1.Instance);
            var match2 = discriminator == null || discriminator(e2.Instance);

            switch (match1)
            {
                case true when match2:
                    return Volatile.Read(ref e1.ActiveRequestsCount) <= Volatile.Read(ref e2.ActiveRequestsCount)
                        ? e1
                        : e2;
                case true:
                    return e1;
            }

            if (match2) return e2;

            Entry? best = null;
            var minRequests = int.MaxValue;

            for (var i = 0; i < len; i++)
            {
                var item = _items[i];
                if (!discriminator(item.Instance)) continue;
                int reqs = Volatile.Read(ref item.ActiveRequestsCount);
                if (reqs < minRequests)
                {
                    minRequests = reqs;
                    best = item;
                }
            }

            return best ?? throw new InvalidOperationException("No instance matches the provided discriminator.");
        }

        #endregion

        #region ExecuteAll Methods

        /// <summary>
        /// Ejecuta una acción en paralelo sobre TODAS las instancias del pool.
        /// </summary>
        public async ValueTask ExecuteAllAsync(Func<T, ValueTask> action)
        {
            Throw.If(_isDisposed != 0, static () => new ObjectDisposedException(nameof(P2CPool<T>)));
            Throw.IfNull(action);

            var tasks = new Task[_items.Length];

            for (int i = 0; i < _items.Length; i++)
            {
                var entry = _items[i];
                tasks[i] = RunEntryAsync(entry, action);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            static async Task RunEntryAsync(Entry entry, Func<T, ValueTask> action)
            {
                Interlocked.Increment(ref entry.ActiveRequestsCount);
                try
                {
                    await action(entry.Instance).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref entry.ActiveRequestsCount);
                }
            }
        }

        /// <summary>
        /// Ejecuta una función en paralelo sobre TODAS las instancias del pool 
        /// y devuelve un array con los resultados proyectados de cada una.
        /// </summary>
        public async ValueTask<TResponse[]> ExecuteAllAsync<TResponse>(Func<T, ValueTask<TResponse>> action)
        {
            Throw.If(_isDisposed != 0, static () => new ObjectDisposedException(nameof(P2CPool<T>)));
            Throw.IfNull(action);

            var tasks = new Task<TResponse>[_items.Length];

            for (int i = 0; i < _items.Length; i++)
            {
                var entry = _items[i];
                tasks[i] = RunEntryAsync(entry, action);
            }

            return await Task.WhenAll(tasks).ConfigureAwait(false);

            static async Task<TResponse> RunEntryAsync(Entry entry, Func<T, ValueTask<TResponse>> action)
            {
                Interlocked.Increment(ref entry.ActiveRequestsCount);
                try
                {
                    return await action(entry.Instance).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref entry.ActiveRequestsCount);
                }
            }
        }

        #endregion

        #region Disposal Pattern

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            foreach (var entry in _items)
            {
                switch (entry.Instance)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}