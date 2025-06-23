namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IHubconResult
    {
        string? ErrorMessage { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; }
    }
}