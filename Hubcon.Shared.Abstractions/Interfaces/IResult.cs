namespace Hubcon
{
    public interface IHubconResult
    {
        string? ErrorMessage { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; }
    }
}