namespace Hubcon
{
    /// <summary>
    /// Indicates the rate limiter policy to be used.
    /// </summary>
    /// <param name="Policy"></param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UseHttpRateLimiterAttribute(string Policy) : Attribute
    {
        /// <summary>
        /// The name of the policy.
        /// </summary>
        public string Policy { get; } = Policy;
    }
}