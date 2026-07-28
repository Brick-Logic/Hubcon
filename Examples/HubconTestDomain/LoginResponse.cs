using System;

namespace HubconTestDomain
{
    public record LoginResponse(string AccessToken, string TokenType, string RefreshToken, long Expires);
}