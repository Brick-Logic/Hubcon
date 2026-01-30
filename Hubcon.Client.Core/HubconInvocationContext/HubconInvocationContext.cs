using System;
using System.Threading;


namespace Hubcon.Client.Core.HubconInvocationContext
{
    public static class HubconContext
    {
        private static readonly AsyncLocal<Context> _current = new();
        public static Context Current => _current.Value ??= new Context();
        public static void UseContext(Context context) => _current.Value = context;

        public class Context
        {
            public bool HasError => Exception != null;
            public bool IsWrapped { get; set; }
            public Exception? Exception { get; set; }
            public bool ResponseIsSet => Response != null;

            private object? Response { get; set; }
            public void SetResponse<T>(IHubconResponse<T> response)
            {
                Response ??= response;
            }

            public IHubconResponse<T>? GetResponse<T>()
            {
                return Response as IHubconResponse<T>;
            }
        }
    }
}