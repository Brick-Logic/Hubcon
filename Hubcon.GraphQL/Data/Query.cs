namespace Hubcon.GraphQL.Data
{
    public class Query
    {
        private readonly bool HealthValue = true;

        [UseProjection]
        public Task<HealthcheckResult> Healthcheck() => Task.FromResult(new HealthcheckResult(HealthValue));

        public record class HealthcheckResult(bool Result);
    }
}
