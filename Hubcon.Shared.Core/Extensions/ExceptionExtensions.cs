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
        /// Throws an <see cref="HubconGenericException"/> if the provided value is null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNull<TValue, TException>([NotNull] TValue? value)
            where TException : Exception, new()
        {
            if (value is null)
            {
                ThrowException<TException>();
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided value is null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNull<TValue, TException>([NotNull] TValue? value, Func<TValue?, TException> exceptionFactory) 
            where TException : Exception 
        {
            if (value is null)
            {
                ThrowFromFactory(value, exceptionFactory);
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided value is null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNull<TValue, TException>([NotNull] TValue? value, Func<TException> exceptionFactory)
            where TException : Exception 
        {
            if (value is null)
            {
                ThrowFromFactory(exceptionFactory);
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
        /// Throws an <see cref="HubconGenericException"/> if the provided values are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfEqual<TValue, TException>([NotNull] TValue value, [NotNull] TValue comparand)
            where TException : Exception, new()
        {
            if (EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowException<TException>();
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided values are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfEqual<TValue, TException>([NotNull] TValue value, [NotNull] TValue comparand, Func<TValue?, TException> exceptionFactory)
            where TException : Exception 
        {
            if (EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowFromFactory(value, exceptionFactory);
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided values are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfEqual<TValue, TException>([NotNull] TValue value, [NotNull] TValue comparand, Func<TException> exceptionFactory)
            where TException : Exception 
        {
            if (EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowFromFactory(exceptionFactory);
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
        /// Throws an <see cref="HubconGenericException"/> if the provided values are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNotEqual<TValue, TException>([NotNull] TValue value, [NotNull] TValue comparand)
            where TException : Exception, new()
        {
            if (!EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowException<TException>();
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided values are not equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNotEqual<TValue, TException>([NotNull] TValue value, [NotNull] TValue comparand,  Func<TValue?, TValue?, TException> exceptionFactory)
            where TException : Exception
        {
            if (!EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowFromFactory(value, comparand, exceptionFactory);
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the provided values are not equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNotEqual<TValue, TException>([NotNull] TValue value, [NotNull] TValue comparand,  Func<TException> exceptionFactory)
            where TException : Exception
        {
            if (!EqualityComparer<TValue>.Default.Equals(value, comparand))
            {
                ThrowFromFactory(exceptionFactory);
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
        public static void If<TException>(bool value)
            where TException : Exception, new()
        {
            if (EqualityComparer<bool>.Default.Equals(value, true))
            {
                ThrowException<TException>();
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the value is true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void If<TException>(bool value, Func<TException> exceptionFactory)
            where TException : Exception
        {
            if (EqualityComparer<bool>.Default.Equals(value, true))
            {
                ThrowFromFactory(exceptionFactory);
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
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the value is true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNot<TException>(bool value)
            where TException : Exception, new()
        {
            if (EqualityComparer<bool>.Default.Equals(value, false))
            {
                ThrowException<TException>();
            }
        }
        
        /// <summary>
        /// Throws an <see cref="HubconGenericException"/> if the value is true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IfNot<TException>(bool value, Func<TException> exceptionFactory)
            where TException : Exception
        {
            if (EqualityComparer<bool>.Default.Equals(value, false))
            {
                ThrowFromFactory(exceptionFactory);
            }
        }

        [DoesNotReturn]
        private static void ThrowGeneric(string message) => throw new HubconGenericException(message);

        [DoesNotReturn]
        private static void ThrowFromFactory<TValue, TException>([NotNull] TValue? value, Func<TValue?, TException> exceptionFactory) where TException : Exception
        {
            throw exceptionFactory.Invoke(value);
        }
        
        [DoesNotReturn]
        private static void ThrowFromFactory<TValue, TException>([NotNull] TValue? value, [NotNull] TValue? value2, Func<TValue?, TValue?, TException> exceptionFactory) where TException : Exception
        {
            throw exceptionFactory.Invoke(value, value2);
        }
        
        [DoesNotReturn]
        private static void ThrowFromFactory<TException>(Func<TException> exceptionFactory) where TException : Exception
        {
            throw exceptionFactory.Invoke();
        }

        [DoesNotReturn]
        private static void ThrowException<T>() where T : Exception, new()
        {
            throw new T();
        }
    }
}