using Castle.DynamicProxy;
using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System;
using System.ComponentModel;

namespace Hubcon.Core.Connectors
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HubconClientBuilder<TICommunicationContract>
        where TICommunicationContract : IHubconControllerContract
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
           "Major Code Smell",
           "S2743:Static fields should not be used in generic types",
           Justification = "The static field by T type is intended.")]
        protected Dictionary<string, MethodInvokeRequest> AvailableMethods { get; } = new();

        protected HubconClientBuilder()
        {
            BuildMethods();
        }

        protected void BuildMethods()
        {
            if (AvailableMethods.Count == 0)
            {
                var TType = typeof(TICommunicationContract);

                if (!TType.IsInterface)
                    throw new ArgumentException($"El tipo {typeof(TICommunicationContract).FullName} no es una interfaz.");

                if (!typeof(IHubconControllerContract).IsAssignableFrom(TType))
                    throw new NotImplementedException($"El tipo {TType.FullName} no implementa la interfaz {nameof(IHubconControllerContract)} ni es un tipo derivado.");

                foreach (var method in TType.GetMethods())
                {
                    var parameters = method.GetParameters();
                    var methodSignature = method.GetMethodSignature();
                    AvailableMethods.TryAdd(methodSignature, new MethodInvokeRequest(methodSignature, TType.Name, new DynamicConverter().SerializeArgsToJson(parameters)));
                }
            }
        }
    }
}
