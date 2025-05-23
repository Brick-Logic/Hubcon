using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Exceptions
{
    public class HubconRemoteException : Exception
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
