using System;
using System.ComponentModel;
#pragma warning disable CS1591
namespace Hubcon.Client.Core.Exceptions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconValidationException : Exception
    {
        public HubconValidationException()
        {
        }

        public HubconValidationException(string? message) : base(message)
        {
        }

        public HubconValidationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
