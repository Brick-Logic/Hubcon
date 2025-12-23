namespace HubconTestDomain
{
    public record LoginCommand(string Username, string Password, bool RememberMe);
}
