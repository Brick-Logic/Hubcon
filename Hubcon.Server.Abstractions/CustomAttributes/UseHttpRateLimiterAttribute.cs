namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UseHttpRateLimiterAttribute(string Policy) : Attribute
    {
        public string Policy { get; } = Policy;
    }
}