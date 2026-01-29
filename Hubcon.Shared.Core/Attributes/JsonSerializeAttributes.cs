using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false)]
    public abstract class JsonSerializeAttribute : Attribute
    {
    }

    public sealed class JsonSerializeAsNumberAttribute : JsonSerializeAttribute
    {
    }
}