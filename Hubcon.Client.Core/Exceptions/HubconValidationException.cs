using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
