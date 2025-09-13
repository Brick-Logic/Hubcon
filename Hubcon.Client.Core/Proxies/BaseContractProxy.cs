using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Cache;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Core.Proxies
{
    public abstract class BaseContractProxy : BaseProxy
    {
        private readonly ImmutableCache<string, (string computedSignature, MethodInfo methodInfo)> Methods = new();
        private string SimpleContractName { get; set; } = string.Empty;

        private Type _contractType = null!;
        private IHubconClient _client = null!;
        private IDynamicConverter _converter = null!;

        public void BuildContractProxy(IHubconClient client, IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;

            _contractType = GetType()
                .GetInterfaces()
                .First(x => typeof(IControllerContract).IsAssignableFrom(x) && x != typeof(IControllerContract));

            var methods = _contractType
                .GetMethods()
                .Where(m => !m.IsSpecialName); // Excluir get_/set_

            SimpleContractName = NamingHelper.GetCleanName(_contractType.Name);

            var env = Environment.GetEnvironmentVariable("HUBCON_OPNAME_DEBUG_ENABLED");
            var useHashed = !bool.TryParse(env, out var parsed) ? true : !parsed;

            foreach (var method in methods)
            {
                var signature = method.GetMethodSignature(false);

                Methods.GetOrAdd(signature, _ => (method.GetMethodSignature(useHashed), method));

                var verb = method.GetCustomAttribute<GetMethodAttribute>();

                if (verb != null && !method.AreParametersValid())
                {
                    throw new HubconGenericException($"Operation '{method.Name}' cannot be used with GET verb as it contains complex or null types. Use primitive types or a DTO class with primitive types instead.");
                }
            }
        }

        private (string computedSignature, MethodInfo methodInfo) GetMethod(string methodSignature)
        {
            if (!Methods.TryGetValue(methodSignature, out (string, MethodInfo) info))
                throw new MissingMethodException($"No se encontró el método '{methodSignature}' en {_contractType}.");

            return info;
        }

        public override Task<T> InvokeAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var (computedSignature, methodInfo) = GetMethod(methodSignature);
            OperationRequest request = new(computedSignature, SimpleContractName, arguments!);
            return _client.SendAsync<T>(request, methodInfo, cancellationToken);
        }

        public override Task CallAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var (computedSignature, methodInfo) = GetMethod(methodSignature);
            OperationRequest request = new(computedSignature, SimpleContractName, arguments!);
            return _client!.CallAsync(request, methodInfo, cancellationToken);
        }

        public override Task<T> IngestAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var (computedSignature, methodInfo) = GetMethod(methodSignature);
            OperationRequest request = new(computedSignature, SimpleContractName, arguments!);
            return _client!.Ingest<T>(request, methodInfo, cancellationToken);
        }

        public override Task IngestAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var (computedSignature, methodInfo) = GetMethod(methodSignature);
            OperationRequest request = new(computedSignature, SimpleContractName, arguments!);
            return _client!.Ingest<JsonElement>(request, methodInfo, cancellationToken);
        }

        public override IAsyncEnumerable<T> StreamAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var (computedSignature, methodInfo) = GetMethod(methodSignature);
            OperationRequest request = new(computedSignature, SimpleContractName, arguments!);
            IAsyncEnumerable<JsonElement> stream = _client.GetStream(request, methodInfo, cancellationToken);
            return _converter.ConvertStream<T>(stream, cancellationToken);
        }
    }
}
