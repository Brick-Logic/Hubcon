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
using System.Diagnostics.CodeAnalysis.Validation;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalRoutingMiddleware(
        IServiceProvider serviceProvider,
        IDynamicConverter converter) : IInternalRoutingMiddleware
    {
        /// <inheritdoc/>
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            var controller = serviceProvider.GetRequiredService(context.Blueprint.ControllerType);
            object? mainTask;
            switch (context.Blueprint.ParameterWrapper)
            {
                case null:
                    mainTask = context.Blueprint!.Invoker?.Invoke(controller, null, context.RequestAborted);
                    break;
                default:
                    if (context.WrappedRequest == null)
                    {
                        var dict = (context.Request.Arguments as Dictionary<string, object>)!;
                        foreach (var parameterType in context.Blueprint.ParameterTypes)
                        {
                            if (!context.Blueprint.ParameterTypes.TryGetValue(parameterType.Key, out var type))
                            {
                                continue;
                            }
                            if (dict.TryGetValue(parameterType.Key, out var item) && item is JsonElement element)
                            {
                                dict[parameterType.Key] = converter.DeserializeJsonElement(element, type)!;
                            }
                            else if (EnumerableTools.IsAsyncEnumerable(dict[parameterType.Key]!)
                                     && EnumerableTools.GetAsyncEnumerableType(dict[parameterType.Key]!) ==
                                     typeof(IAsyncEnumerable<JsonElement>))
                            {
                                dict[parameterType.Key] = EnumerableTools.ConvertAsyncEnumerableDynamic(
                                    type,
                                    ((IAsyncEnumerable<JsonElement>)dict[parameterType.Key]!),
                                    converter);
                            }
                        }
                        
                        context.Request.AssignArguments(dict);
                    }

                    var wrapper = context.WrappedRequest ??
                                  context.Blueprint.ParameterWrapper.GetWrapped(context.Request.Arguments);
                    var validationResults = new List<ValidationResult>();
                    var validationContext = new ValidationContext(wrapper);
                    if (!Validator.TryValidateObject(wrapper, validationContext, validationResults, true))
                    {
                        var errors = validationResults
                            .SelectMany(static r => r is CompositeValidationResult comp ? comp.Results : new[] { r })
                            .ToDictionary(
                                static k => k.MemberNames.FirstOrDefault() ?? "error",
                                static v => new[] { v.ErrorMessage ?? "Invalid value" }
                            );

                        context.Response = HubconResponse.BadRequest(errors, error: "Validation errors detected.");
                        return;
                    }
                    
                    mainTask = context.Blueprint!.Invoker?.Invoke(controller, wrapper, context.RequestAborted);
                    break;
            }

            context.Response = await context.ResultHandler.Invoke(mainTask);
            await next();
        }
    }
}