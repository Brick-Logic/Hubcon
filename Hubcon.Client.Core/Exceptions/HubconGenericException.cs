using System;
using System.ComponentModel;

namespace Hubcon.Client.Core.Exceptions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconGenericException : Exception
    {
        public HubconGenericException()
        {
        }

        public HubconGenericException(string? message) : base(message)
        {
        }

        public HubconGenericException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
