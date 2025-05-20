
namespace Hubcon.Core.Authentication
{
    public interface IPersistedSession
    {
        string AccessToken { get; set; }
        DateTime ExpiresAt { get; set; }
        string RefreshToken { get; set; }
    }
}