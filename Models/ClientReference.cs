namespace Hubcon
{
    public class ClientReference
    {
        public string Id { get; }
        public object? ClientInfo { get; set; }

        public ClientReference(string id)
        {
            Id = id;
        }

        public ClientReference(string id, object? clientInfo)
        {
            Id = id;
            ClientInfo = clientInfo;
        }   
    }
    public class ClientReference<TIClientController> : ClientReference
    {
        public TIClientController ClientController { get; init; }

        public ClientReference(string id, TIClientController clientController) : base(id)
        {
            ClientController = clientController;
        }

        public ClientReference(string id, object? clientInfo, TIClientController clientController) : base(id, clientInfo)
        {
            ClientController = clientController;
        }
    }

}
