using Hubcon;
using Hubcon.Shared.Abstractions.Attributes;
using HubconTestClient.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HubconTestClient.Contracts
{
    public interface IOpenAIContract : IControllerContract
    {
        [HttpPost("/v1/responses")]
        public Task<OpenAIResponse> CreateModelResponse([AsBody] CreateResponseCommand command);

        [HttpGet("/v1/responses/{id}")]
        public Task<OpenAIResponse> GetModelResponse();

        [HttpGet("/v1/responses/{id}/input_items")]
        public Task<OpenAIList<OpenAIMessage>> GetModelResponseInputs(string id);

        [HttpDelete("/v1/responses/{id}")]
        public Task<OpenAIResponse> DeleteModelResponse(string id);

        [HttpPost("/v1/responses")]
        [ParseSseMessage("data:", "")]
        [ParseSseMessage("event:", "event")]
        [ParseEndSseMessage("[DONE]")]
        public IAsyncEnumerable<OpenAIStreamEvent> GetResponseStream([AsBody] OpenAIStreamRequest request);
    }
}