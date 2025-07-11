using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class SubscriptionSettings(
        int? channelCapacity = null,
        BoundedChannelFullMode? channelFullMode = null,
        TimeSpan? throttleDelay = null)
    {
        public int ChannelCapacity { get; private set; } = channelCapacity ?? 1000;
        public BoundedChannelFullMode ChannelFullMode { get; private set; } = channelFullMode ?? BoundedChannelFullMode.Wait;
        public TimeSpan ThrottleDelay { get; private set; } = throttleDelay ?? TimeSpan.FromMilliseconds(8);

        public static SubscriptionSettings Default { get; } = new();
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SubscriptionSettingsAttribute : Attribute
    {
        public SubscriptionSettings Settings { get; }

        public SubscriptionSettingsAttribute(
            int ChannelCapacity = 1000,
            BoundedChannelFullMode ChannelFullMode = BoundedChannelFullMode.Wait,
            int ThrottleDelay = 8)
        {
            var delay = ThrottleDelay == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ThrottleDelay);
            Settings = new SubscriptionSettings(ChannelCapacity, ChannelFullMode, delay);
        }
    }
}
