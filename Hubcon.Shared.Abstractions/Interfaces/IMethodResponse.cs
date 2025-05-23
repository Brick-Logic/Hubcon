namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationResult : IResponse
    {
        public object? Data { get; }
    }

    public interface IOperationResponse<T> : IOperationResult
    {
        public new T? Data { get; }
    }

    public interface IObjectOperationResponse : IOperationResponse<object>, IOperationResult, IResponse
    {
    }
}