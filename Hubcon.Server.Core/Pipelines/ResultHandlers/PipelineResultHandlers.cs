using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Core.Pipelines.ResultHandlers
{
    internal static class PipelineResultHandlers
    {
        internal static async Task<HubconResponse> ResultHandler(object? result)
        {
            if (result is null)
            {
                return HubconResponse.Ok();
            }
            else
            {
                if (result is IResponse converted)
                    return converted.GetBoxed();

                return HubconResponse.Ok(result);
            }
        }

        internal static async Task<HubconResponse> NoResultHandler(object? result)
        {
            if (result is Task task)
                await task;

            return HubconResponse.Ok();
        }

        internal static async Task<HubconResponse> StreamResultHandler(object? result)
        {
            if (result is IAsyncEnumerable<object?> sub)
            {
                return HubconResponse.Ok(sub);
            }
            else
            {
                return HubconResponse.InternalError();
            }
        }

        internal static async Task<HubconResponse> WithResultHandler(object? result)
        {
            if (result is Task task)
            {
                var response = await GetTaskResultAsync(task);

                if (response is IResponse converted)
                    return converted.GetBoxed();

                return HubconResponse.Ok(response);
            }
            else
            {
                if (result is IResponse converted)
                    return converted.GetBoxed();

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
