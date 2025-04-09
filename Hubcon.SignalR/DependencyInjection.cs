using Hubcon.Core;
using Hubcon.SignalR.Server;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.SignalR
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder? UseHubconSignalR(this WebApplicationBuilder e)
        {
            MessagePackSerializerOptions mpOptions = MessagePackSerializerOptions.Standard
                .WithResolver(CompositeResolver.Create(
                    ContractlessStandardResolver.Instance // Usa un resolver sin atributos
                ));

            e.Services.AddSignalR()
                .AddMessagePackProtocol(options =>
                 {
                     options.SerializerOptions = mpOptions;
                 });

            e.Services.AddHubcon(services =>
            {
                services.AddScoped(typeof(SignalRServerCommunicationHandler<>));
                services.AddScoped(typeof(HubConnectionBuilder));
            });


            return e;
        }
    }
}
