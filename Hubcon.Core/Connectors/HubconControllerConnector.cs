using Hubcon.Core.Extensions;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;
using System.ComponentModel;

namespace Hubcon.Core.Connectors
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HubconClientBuilder<TICommunicationContract>
        where TICommunicationContract : ICommunicationContract
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

                if (!typeof(ICommunicationContract).IsAssignableFrom(TType))
                    throw new NotImplementedException($"El tipo {TType.FullName} no implementa la interfaz {nameof(ICommunicationContract)} ni es un tipo derivado.");

                foreach (var method in TType.GetMethods())
                {
                    var parameters = method.GetParameters();
                    var methodSignature = method.GetMethodSignature();
                    AvailableMethods.TryAdd(methodSignature, new MethodInvokeRequest(methodSignature, null, parameters));
                }
            }
        }
    }
}
