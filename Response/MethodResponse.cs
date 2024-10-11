namespace Hubcon.Response
{
    public record class MethodResponse(bool Success, object? Data = null);
}
