using System;

namespace Hubcon.Shared.Abstractions.Attributes
{
    internal sealed class UseCommunicationHandlerAttribute : Attribute
    {
        public Type HandlerType { get; }

        public UseCommunicationHandlerAttribute(Type handlerType)
        {
            HandlerType = handlerType;
        }
    }
}
