using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
