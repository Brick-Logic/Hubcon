using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Exceptions
{
    public class HubconGenericException : Exception
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
