namespace Hubcon.Websockets.Shared
{
    public enum MessageType
    {
        connection_init,
        connection_ack,
        error,
        ack,
        operation_invoke,
        operation_response,
        ping,
        pong,
        subscription_init,
        subscription_event_data,
        subscription_event_ack,
        subscription_cancel,
        ingest_init,
        ingest_init_ack,
        ingest_data,
        ingest_data_ack,
        ingest_complete,
        ingest_data_with_ack,
        operation_call
    }
}