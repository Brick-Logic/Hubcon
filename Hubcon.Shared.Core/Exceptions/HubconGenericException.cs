using System;
using System.ComponentModel;
#pragma warning disable CS1591
namespace Hubcon
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
