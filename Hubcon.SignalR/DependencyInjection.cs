using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
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
            return e;
        }

        
    }
}
