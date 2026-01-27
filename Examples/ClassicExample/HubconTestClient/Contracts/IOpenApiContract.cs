using Hubcon;
using HubconTestClient.Models;

namespace HubconTestClient.Contracts
{
    public interface IOpenAIContract : IControllerContract
    {
        [HttpPost("/v1/responses")]
        public Task<OpenAIResponse> CreateModelResponse([AsBody] CreateResponseCommand command);

        [HttpGet("/v1/responses/{id}")]
        public Task<OpenAIResponse> GetModelResponse(string id);

        [HttpGet("/v1/responses/{id}/input_items")]
        public Task<OpenAIList<OpenAIMessage>> GetModelResponseInputs(string id);

        [HttpDelete("/v1/responses/{id}")]
        public Task<OpenAIResponse> DeleteModelResponse(string id);

        [HttpPost("/v1/responses")]
        public IAsyncEnumerable<OpenAIStreamEvent> GetResponseStream([AsBody] OpenAIStreamRequest request);
    }
}
