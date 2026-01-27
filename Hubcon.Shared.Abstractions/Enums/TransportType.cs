namespace Hubcon
{
    public enum TransportType
    {
        Default, // Will decide based on contract configuration, or http by default
        Http,
        Websockets
    }
}
