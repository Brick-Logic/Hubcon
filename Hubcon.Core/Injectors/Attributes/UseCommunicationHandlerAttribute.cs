using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Injectors.Attributes
{
    public class UseCommunicationHandlerAttribute : Attribute
    {
        public Type HandlerType { get; }

        public UseCommunicationHandlerAttribute(Type handlerType)
        {
            HandlerType = handlerType;
        }
    }
}
