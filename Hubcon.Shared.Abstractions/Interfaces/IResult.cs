namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IResult
    {
        string? ErrorMessage { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; }
    }
}