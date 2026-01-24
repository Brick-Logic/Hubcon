using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon.Client.Core.HubconInvocationContext
{
    public static class HubconContext
    {
        private static readonly AsyncLocal<Context> _current = new();
        public static Context Current => _current.Value ??= new Context();
        public static void UseContext(Context context) => _current.Value = context;

        public class Context
        {
            public Exception? LastError { get; set; }
            public bool HasError => LastError != null;
            public bool IsWrapped { get; set; }
            public object? Response { get; set; }
            public Exception? Exception { get; set; }
        }
    }
}