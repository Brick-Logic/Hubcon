namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IPersistedSession
    {
        string AccessToken { get; set; }
        DateTime ExpiresAt { get; set; }
        string? RefreshToken { get; set; }
    }
}