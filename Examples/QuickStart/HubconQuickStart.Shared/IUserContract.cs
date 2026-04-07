using Hubcon;

namespace HubconQuickStart.Shared;

[HttpTransport]
public interface IUserContract : IControllerContract
{
    Task<string> TestHubcon(string message);
}