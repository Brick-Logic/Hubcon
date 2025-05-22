using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Attributes;

namespace Hubcon.Client
{
    internal class ServerModuleConfiguration(IClientBuilder builder) : IServerModuleConfiguration
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
            builder.LoadContractProxy(contractType);
        }

        public IServerModuleConfiguration AddMiddleware<T>() where T : IMiddleware
        {
            if (builder.Contracts.Any(x => x == typeof(T)))
                return this;

            builder.Contracts.Add(typeof(T));
            return this;
        }

        public IServerModuleConfiguration UseAuthenticationManager<T>() where T : IAuthenticationManager
        {
            builder.UseAuthenticationManager<T>();
            return this;
        }
    }
}