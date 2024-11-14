using Hubcon.Handlers;
using Hubcon.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.ComponentModel;

namespace Hubcon.Controllers
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class HubController : IHubController
    {
        protected MethodHandler handler;

        protected HubController()
        {
            handler = new MethodHandler();
        }

        protected void Build(HubConnection hubConnection)
        {
            Type derivedType = GetType();
            if (!typeof(IHubController).IsAssignableFrom(derivedType))
                throw new NotImplementedException($"El tipo {derivedType.FullName} no implementa la interfaz {nameof(IHubController)} o un tipo derivado.");

            handler.BuildMethods(this, derivedType, (methodSignature, methodInfo, delegado) =>
            {
                if (methodInfo.ReturnType == typeof(void))
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task>)handler.HandleWithoutResultAsync);
                else if (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task<MethodResponse>>)handler.HandleWithResultAsync);
                else if (methodInfo.ReturnType == typeof(Task))
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task>)handler.HandleWithoutResultAsync);
                else
                    hubConnection?.On($"{methodSignature}", (Func<MethodInvokeInfo, Task<MethodResponse>>)handler.HandleWithResultAsync);
            });
        }
    }
}
