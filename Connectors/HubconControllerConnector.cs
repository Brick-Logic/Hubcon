using Hubcon.Extensions;
using Hubcon.Models;
using System.ComponentModel;

namespace Hubcon.Connectors
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HubconClientBuilder<TIHubController>
        where TIHubController : IHubController
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
           "Major Code Smell",
           "S2743:Static fields should not be used in generic types",
           Justification = "The static field by T type is intended.")]
        protected Dictionary<string, MethodInvokeInfo> AvailableMethods { get; } = [];

        protected HubconClientBuilder()
        {
            BuildMethods();
        }

        protected void BuildMethods()
        {
            if (AvailableMethods.Count == 0)
            {
                var TType = typeof(TIHubController);

                if (!TType.IsInterface)
                    throw new ArgumentException($"El tipo {typeof(TIHubController).FullName} no es una interfaz.");

                if (!typeof(IHubController).IsAssignableFrom(TType))
                    throw new NotImplementedException($"El tipo {TType.FullName} no implementa la interfaz {nameof(IHubController)} ni es un tipo derivado.");

                foreach (var method in TType.GetMethods())
                {
                    var parameters = method.GetParameters();
                    var methodSignature = method.GetMethodSignature();
                    AvailableMethods.TryAdd(methodSignature, new MethodInvokeInfo(methodSignature, parameters));
                }
            }
        }
    }
}
