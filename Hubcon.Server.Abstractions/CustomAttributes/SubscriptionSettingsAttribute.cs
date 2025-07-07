using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class SubscriptionSettings(TimeSpan? throttleDelay = null)
    {
        public TimeSpan ThrottleDelay { get; init; } = throttleDelay ?? TimeSpan.FromMilliseconds(1);

        public static SubscriptionSettings Default => new();
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SubscriptionSettingsAttribute : Attribute
    {
        public SubscriptionSettings Settings { get; }

        public SubscriptionSettingsAttribute(int ThrottleDelayMilliseconds = 16)
        {
            var delay = ThrottleDelayMilliseconds == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ThrottleDelayMilliseconds);
            Settings = new SubscriptionSettings(delay);
        }
    }
}
