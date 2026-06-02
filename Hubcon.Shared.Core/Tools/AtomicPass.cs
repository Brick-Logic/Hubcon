using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Hubcon.Shared.Core.Tools
{
    /// <summary>
    /// A simple atomic pass implementation using Interlocked operations.
    /// </summary>
    public sealed class AtomicPass
    {
        private int _pass;

        /// <summary>
        /// Attempts to acquire a pass. Returns true if the pass was successfully acquired, false if it was already set.
        /// </summary>
        /// <returns></returns>
        public bool TryAcquirePass()
        {
            if (Interlocked.CompareExchange(ref _pass, 1, 0) == 1)
                return false;

            return true;
        }

        /// <summary>
        /// Provides a way to check if the pass was previsouly acquired without modifying its state.
        /// </summary>
        public bool WasAcquired => Volatile.Read(ref _pass) == 1;
    }
}