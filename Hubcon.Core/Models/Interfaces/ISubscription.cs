using System.Reflection;

namespace Hubcon.Core.Models.Interfaces
{
    public delegate void HubconEventHandler(object? eventValue);

    public interface ISubscription
    {
        public event HubconEventHandler? OnEventReceived;
        PropertyInfo Property { get; }
        void Build();
        void Subscribe();
        void Unsubscribe();
        public bool IsSubscribed { get; }
        public void AddHandler(HubconEventHandler handler);
        public void RemoveHandler(HubconEventHandler handler);
        public void Emit(object? eventValue);

        public static ISubscription operator +(ISubscription handler, HubconEventHandler hubconEventHandler)
        {
            handler.OnEventReceived += hubconEventHandler;
            return handler;
        }

        public static ISubscription operator -(ISubscription handler, HubconEventHandler hubconEventHandler)
        {
            handler.OnEventReceived -= hubconEventHandler;
            return handler;
        }
    }
}
