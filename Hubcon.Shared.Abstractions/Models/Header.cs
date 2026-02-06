using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Shared.Abstractions.Models
{
    public sealed class Header
    {
        public Header(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }
        public string Value { get; }
    }
}
