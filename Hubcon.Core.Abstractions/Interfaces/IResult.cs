namespace Hubcon.Core.Authentication
{
    public interface IResult
    {
        string? ErrorMessage { get; }
        bool IsFailure { get; }
        bool IsSuccess { get; }
    }
}