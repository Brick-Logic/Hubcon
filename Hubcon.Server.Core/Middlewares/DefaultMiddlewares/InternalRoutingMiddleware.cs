using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalRoutingMiddleware(
        IServiceProvider serviceProvider,
        IDynamicConverter dynamicConverter) : IInternalRoutingMiddleware
    {
        /// <inheritdoc/>
        public async Task Execute(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, PipelineDelegate next)
        {
            var dict = context.Request.Arguments.ToDictionary();

            if (context.Blueprint.Kind == OperationKind.Ingest)
            {
                foreach (var kvp in context.Blueprint!.ParameterTypes)
                {
                    if (!context.Blueprint!.ParameterTypes.TryGetValue(kvp.Key, out var type))
                    {
                        continue;
                    }
                    if (dict.TryGetValue(kvp.Key, out var item) && item is JsonElement element)
                    {
                        dict[kvp.Key] = dynamicConverter.DeserializeJsonElement(element, type)!;
                    }
                    else if (EnumerableTools.IsAsyncEnumerable(dict[kvp.Key]!)
                        && EnumerableTools.GetAsyncEnumerableType(dict[kvp.Key]!) == typeof(IAsyncEnumerable<JsonElement>))
                    {
                        dict[kvp.Key] = EnumerableTools.ConvertAsyncEnumerableDynamic(
                            type,
                            ((IAsyncEnumerable<JsonElement>)dict[kvp.Key]!),
                            dynamicConverter);

                        continue;
                    }
                }
            }
            else
            {
                foreach (var kvp in context.Blueprint!.ParameterTypes)
                {
                    if (context.Blueprint!.ParameterTypes.TryGetValue(kvp.Key, out var type)
                        && dict.TryGetValue(kvp.Key, out var item)
                        && item is JsonElement element)
                    {
                        dict[kvp.Key] = dynamicConverter.DeserializeJsonElement(element, type)!;
                    }
                }
            }

            var controller = context.Blueprint!.ControllerFactory.Invoke(serviceProvider, null);

            var wrapper = Activator.CreateInstance(context.Blueprint!.CallWrapperType!)!;
            context.Blueprint!.WrapperMapper!.Invoke(dict, wrapper, default);

            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(wrapper);
            if (!Validator.TryValidateObject(wrapper, validationContext, validationResults, true))
            {
                var errors = validationResults.ToDictionary(
                    k => k.MemberNames.FirstOrDefault() ?? "error",
                    v => new[] { v.ErrorMessage ?? "Invalid value" }
                );

                context.Response = HubconResponse.BadRequest(errors, error: "Validation errors detected.");
                return;
            }


            object? result = null;

            try
            {
                result = context.Blueprint!.InvokeDelegate?.Invoke(controller, wrapper, context.RequestAborted);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
            }

            var response = await resultHandler.Invoke(result);
            context.Response = (response as IHubconResponse)!;
            await next();
        }
    }
}
