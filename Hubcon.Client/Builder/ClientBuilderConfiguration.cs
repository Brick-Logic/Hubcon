using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client.Builder
{
    internal class ServerModuleConfiguration(IClientBuilder builder, IServiceCollection services) : IServerModuleConfiguration
    {
        public IServerModuleConfiguration WithBaseUrl(string hostUrl)
        {
            if (builder.BaseUri != null)
                return this;

            builder.BaseUri = new Uri(hostUrl);
            return this;
        }

        public IServerModuleConfiguration UseInsecureConnection()
        {
            builder.UseSecureConnection = false;
            return this;
        }

        public IServerModuleConfiguration AddContract<T>() where T : IControllerContract
        {
            if (builder.Contracts.Any(x => x == typeof(T)))
                return this;

            LoadContractProxy(typeof(T));
            builder.Contracts.Add(typeof(T));
            return this;
        }

        private void LoadContractProxy(Type contractType)
        {
            builder.LoadContractProxy(contractType, services);
        }

        public IServerModuleConfiguration UseAuthenticationManager<T>() where T : IAuthenticationManager
        {
            builder.UseAuthenticationManager<T>(services);
            return this;
        }
    }
}