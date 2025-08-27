namespace Hubcon.Server.Abstractions.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class SubscriptionAuthorizeAttribute : Microsoft.AspNetCore.Authorization.AuthorizeAttribute
    {
    }
}