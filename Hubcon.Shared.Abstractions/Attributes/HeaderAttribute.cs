using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Adds a header to a Hubcon endpoint or contract.
    /// </summary>
    public sealed class HeaderAttribute : Attribute
    {
        public bool IsStatic { get; private set; }
        public string Key { get; }
        public string? Value { get; }

        /// <summary>
        /// Used for dynamic headers by key.
        /// </summary>
        /// <param name="key"></param>
        public HeaderAttribute(string key)
        {
            IsStatic = false;
            Key = key;
        }

        /// <summary>
        /// Used for static headers.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public HeaderAttribute(string key, string value)
        {
            IsStatic = true;
            Key = key;
            Value = value;
        }
    }
}
