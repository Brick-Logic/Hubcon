namespace Hubcon.GraphQL.Data
{
    public class Query
    {
        [UseProjection]
        public async Task<HealthcheckResult> Healthcheck() => new HealthcheckResult(true);

        public record class HealthcheckResult(bool Result);
    }
}
