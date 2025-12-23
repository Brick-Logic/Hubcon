namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IOnStreamReceived
    {
        public Delegate? GetCurrentEvent();
    }
}
