using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Core.Pipelines.ResultHandlers
{
    internal static class PipelineResultHandlers
    {
        internal static Task<HubconResponse> ResultHandler(object? result)
        {
            if (result is null)
            {
                return Task.FromResult(HubconResponse.Ok());
            }
            else
            {
                if (result is HubconResponse converted)
                    return Task.FromResult(converted);

                return Task.FromResult(HubconResponse.Ok(result));
            }
        }

        internal static async Task<HubconResponse> NoResultHandler(object? result)
        {
            if (result is Task task)
                await task;

            return HubconResponse.Ok();
        }

        internal static Task<HubconResponse> StreamResultHandler(object? result)
        {
            if (result is IAsyncEnumerable<object?> sub)
            {
                return Task.FromResult(HubconResponse.Ok(sub));
            }
            else
            {
                return Task.FromResult(HubconResponse.InternalError());
            }
        }

        internal static async Task<HubconResponse> WithResultHandler(object? result)
        {
            if (result is Task task)
            {
                var response = await GetTaskResultAsync(task);

                if (response is HubconResponse converted)
                    return converted;

                return HubconResponse.Ok(response);
            }
            else
            {
                if (result is HubconResponse converted)
                    return converted;

                return HubconResponse.Ok(result);
            }
        }

        private static async Task<object?> GetTaskResultAsync(Task taskObject)
        {
            await taskObject;

            var taskType = taskObject.GetType();

            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                var result = resultProperty?.GetValue(taskObject);

                return result;
            }

            return null;
        }
    }
}
