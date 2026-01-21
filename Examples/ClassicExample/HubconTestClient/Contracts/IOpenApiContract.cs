using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using HubconTestClient.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HubconTestClient.Contracts
{
    public interface IOpenApiContract : IControllerContract
    {
        [HttpPost("/v1/responses")]
        public Task<OpenAIResponse> CreateModelResponse([AsBody] CreateResponseCommand command);

        [HttpGet("/v1/responses/{id}")]
        public Task<OpenAIResponse> GetModelResponse(string id);

        [HttpGet("/v1/responses/{id}/input_items")]
        public Task<OpenAIList<OpenAIMessage>> GetModelResponseInputs(string id);

        [HttpDelete("/v1/responses/{id}")]
        public Task<OpenAIDeleteResponse> DeleteModelResponse(string id);
    }
}
