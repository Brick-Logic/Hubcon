using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IPersistedSession
    {
        string? AccessToken { get; set; }
        long? ExpiresAt { get; set; }
        string? RefreshToken { get; set; }
        string? TokenType { get; set; }
    }
}