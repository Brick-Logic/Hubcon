using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Cache;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Core.Proxies
{
    public abstract class BaseContractProxy : BaseProxy
    {
        private readonly ImmutableCache<(Type, string), MethodInfo> Methods = new();

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

            foreach (var method in methods)
            {
                var signature = method.GetMethodSignature();
                Methods.GetOrAdd((_contractType, signature), _ => method);
            }
        }

        private MethodInfo GetMethod(string methodSignature)
        {
            if (!Methods.TryGetValue((_contractType, methodSignature), out var method))
                throw new MissingMethodException($"No se encontró el método '{methodSignature}' en {_contractType}.");

            return method;
        }

        public override Task<T> InvokeAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var method = GetMethod(methodSignature);
            OperationRequest request = new(methodSignature, _contractType.Name, arguments);
            return _client.SendAsync<T>(request, method, cancellationToken);
        }

        public override Task CallAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var method = GetMethod(methodSignature);
            OperationRequest request = new(methodSignature, _contractType.Name, arguments!);
            return _client!.CallAsync(request, method, cancellationToken);
        }

        public override Task<T> IngestAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var method = GetMethod(methodSignature);
            OperationRequest request = new(methodSignature, _contractType.Name, arguments!);
            return _client!.Ingest<T>(request, method, cancellationToken);
        }

        public override Task IngestAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var method = GetMethod(methodSignature);
            OperationRequest request = new(methodSignature, _contractType.Name, arguments!);
            return _client!.Ingest<JsonElement>(request, method, cancellationToken);
        }

        public override IAsyncEnumerable<T> StreamAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            var method = GetMethod(methodSignature);
            OperationRequest request = new(methodSignature, _contractType.Name, arguments!);
            IAsyncEnumerable<JsonElement> stream = _client.GetStream(request, method, cancellationToken);
            return _converter.ConvertStream<T>(stream, cancellationToken);
        }
    }
}
