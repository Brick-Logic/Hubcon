#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class JsonDefaultAttribute : Attribute
    {
    }
}
