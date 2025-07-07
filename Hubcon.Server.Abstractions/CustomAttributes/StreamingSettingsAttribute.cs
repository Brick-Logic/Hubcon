using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class StreamingSettings(TimeSpan? throttleDelay = null)
    {
        public TimeSpan ThrottleDelay { get; init; } = throttleDelay ?? TimeSpan.FromMilliseconds(1);

        public static StreamingSettings Default => new();
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class StreamingSettingsAttribute : Attribute
    {
        public StreamingSettings Settings { get; }

        public StreamingSettingsAttribute(int ThrottleDelayMilliseconds = 16)
        {
            var delay = ThrottleDelayMilliseconds == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ThrottleDelayMilliseconds);
            Settings = new StreamingSettings(delay);
        }
    }
}
