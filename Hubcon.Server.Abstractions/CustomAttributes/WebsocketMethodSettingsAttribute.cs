using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class WebsocketMethodSettings(TimeSpan? throttleDelay = null)
    {
        public TimeSpan ThrottleDelay { get; private set; } = throttleDelay ?? TimeSpan.FromMilliseconds(1);

        public static WebsocketMethodSettings Default => new();
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class WebsocketMethodSettingsAttribute : Attribute
    {
        public WebsocketMethodSettings Settings { get; }

        public WebsocketMethodSettingsAttribute(int ThrottleDelay = 16)
        {
            var delay = ThrottleDelay == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ThrottleDelay);
            Settings = new WebsocketMethodSettings(delay);
        }
    }
}
