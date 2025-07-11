using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{ 
    public sealed class MethodSettings(TimeSpan? throttleDelay = null)
    {
        public TimeSpan ThrottleDelay { get; private set; } = throttleDelay ?? TimeSpan.FromMilliseconds(8);

        public static MethodSettingsAttribute Default { get; } = new();
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class MethodSettingsAttribute : Attribute
    {
        public MethodSettings Settings { get; }

        public MethodSettingsAttribute(int ThrottleDelay = 8)
        {
            var delay = ThrottleDelay == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ThrottleDelay);
            Settings = new MethodSettings(delay);
        }
    }
}
