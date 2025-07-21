namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public record class BaseMessage
    {
        public Guid Id { get; set; }
        public MessageType Type { get; set; } = default!;

        public BaseMessage()
        {
            
        }

        protected BaseMessage(MessageType type, Guid id)
        {

            Type = type;
            Id = id;
        }
    }
}