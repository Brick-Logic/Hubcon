using System;
using System.ComponentModel;
#pragma warning disable CS1591
namespace Hubcon.Client.Core.Exceptions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconRemoteException : Exception
    {
        public HubconRemoteException()
        {
        }

        public HubconRemoteException(string? message) : base(message)
        {
        }

        public HubconRemoteException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
