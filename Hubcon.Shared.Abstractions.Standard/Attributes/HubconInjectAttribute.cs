using System;

namespace Hubcon.Shared.Abstractions.Standard.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class HubconInjectAttribute : Attribute
    {
        public Type Type { get; }

        public HubconInjectAttribute(Type type = null)
        {
            Type = type;
        }
    }
}
