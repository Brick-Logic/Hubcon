using System;
using System.Threading;
using Hubcon.Shared.Core.Extensions;

namespace Hubcon.Shared.Core.Tools
{
    /// <summary>
    /// A high-performance lock-free atomic counter with lower and upper bounds.
    /// Ideal for rate limiting, connection throttling, and buffer pooling.
    /// </summary>
    public sealed class AtomicCounter
    {
        private readonly int _maxCount;
        private int _count;

        public AtomicCounter(int maxCount, int initialCount = 0)
        {
            Throw.If(maxCount <= 0, nameof(maxCount),
                static x => new ArgumentOutOfRangeException(x, "Parameter maxCount cannot be 0 or negative."));
            Throw.If(initialCount <= 0, nameof(initialCount),
                static x => new ArgumentOutOfRangeException(x, "Parameter initialCount cannot be negative."));
            Throw.If(initialCount > maxCount, (nameof(maxCount), nameof(initialCount)),
                static x => new ArgumentOutOfRangeException($"{x.Item1}, {x.Item2}", "Initial count cannot be greater than max count."));

            _maxCount = maxCount;
            _count = initialCount;
        }

        /// <summary>
        /// Gets the maximum allowed count.
        /// </summary>
        public int MaxCount => _maxCount;

        /// <summary>
        /// Gets the current count atomically.
        /// </summary>
        public int Value => Volatile.Read(ref _count);

        /// <summary>
        /// Attempts to increment the counter.
        /// Returns true if incremented successfully; false if it reached the maximum limit.
        /// </summary>
        public bool TryIncrement()
        {
            while (true)
            {
                int current = Volatile.Read(ref _count);

                if (current >= _maxCount)
                    return false;

                if (Interlocked.CompareExchange(ref _count, current + 1, current) == current)
                    return true;
            }
        }

        /// <summary>
        /// Decrements the counter, clamping the result to a minimum of 0.
        /// Returns the new decremented value.
        /// </summary>
        public int Decrement()
        {
            while (true)
            {
                var current = Volatile.Read(ref _count);

                if (current <= 0)
                    return 0;

                var newValue = current - 1;
                if (Interlocked.CompareExchange(ref _count, newValue, current) == current)
                    return newValue;
            }
        }

        /// <summary>
        /// Resets the counter back to zero atomically.
        /// </summary>
        public void Reset() => Interlocked.Exchange(ref _count, 0);
    }
}