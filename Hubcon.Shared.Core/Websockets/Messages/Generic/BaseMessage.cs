namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public record class BaseMessage
    {
        public string Id { get; set; }
        public MessageType Type { get; set; } = default!;

        public BaseMessage()
        {
            
        }

        protected BaseMessage(MessageType type, string id)
        {

            Type = type;
            Id = id;
        }
    }
}