using System;

namespace Hubcon.Shared.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class GetMethodAttribute : Attribute
    {
        public GetMethodAttribute()
        {
        }
    }
}

