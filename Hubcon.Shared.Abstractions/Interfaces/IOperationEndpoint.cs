namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationEndpoint
    {
        string ContractName { get; }
        string OperationName { get; }

        public void SetOperationName(string operationName);
        public void AddTransportPrefix(ITransportAttribute transport);
    }
}
