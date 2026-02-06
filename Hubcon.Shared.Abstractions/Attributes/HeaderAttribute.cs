using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Shared.Abstractions.Attributes
{
    public sealed class HeaderAttribute : Attribute
    {
        public bool IsStatic { get; private set; }
        public string Key { get; }
        public string? Value { get; }

        public HeaderAttribute(string key)
        {
            IsStatic = false;
            Key = key;
        }

        public HeaderAttribute(string key, string value)
        {
            IsStatic = true;
            Key = key;
            Value = value;
        }
    }
}
