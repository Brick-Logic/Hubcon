using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    internal sealed class WebsocketMethodSettings(TimeSpan? throttleDelay = null)
    {
        public TimeSpan ThrottleDelay { get; private set; } = throttleDelay ?? TimeSpan.FromMilliseconds(8);

        public static WebsocketMethodSettings Default { get; } = new();
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class WebsocketMethodSettingsAttribute : Attribute
    {
        public WebsocketMethodSettings Settings { get; }

        public WebsocketMethodSettingsAttribute(int ThrottleDelay = 8)
        {
            var delay = ThrottleDelay == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ThrottleDelay);
            Settings = new WebsocketMethodSettings(delay);
        }
    }
}
