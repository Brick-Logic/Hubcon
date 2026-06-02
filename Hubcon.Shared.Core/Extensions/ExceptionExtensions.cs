using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Hubcon.Shared.Core.Extensions
{
    /// <summary>
    /// Exception throwing tools for hot paths.
    /// </summary>
    public static class Throw
    {
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided value is null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNull<TValue>([NotNull] TValue? value, string message = "Argument was null.")
        {
            if (value is null)
            {
                ThrowGeneric(message);
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided values are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfEqual<TValue>([NotNull] TValue value, [NotNull] TValue comparand, string message = "Values must not be equal.")
        {
            if (EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowGeneric(message);
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided values are not equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNotEqual<TValue>([NotNull] TValue value, [NotNull] TValue comparand, string message = "Values are not equal.")
        {
            if (!EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowGeneric(message);
            }
        }

        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the value is true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void If(bool value, string message = "Values are not equal.")
        {
            if (EqualityComparer<bool>.Default.Equals(value, true))
            {
                ThrowGeneric(message);
            }
        }

        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the value is true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNot(bool value, string message = "Values are not equal.")
        {
            if (EqualityComparer<bool>.Default.Equals(value, false))
            {
                ThrowGeneric(message);
            }
        }

        [DoesNotReturn]
        private static void ThrowGeneric(string message) => throw new HubconGenericException(message);
    }
}