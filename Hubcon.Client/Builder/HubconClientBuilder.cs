using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Registries;
using Hubcon.Client.Core.Subscriptions;
using Hubcon.Client.Core.Transports;
using Hubcon.Client.Integration.Client;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Attributes;
using Hubcon.Shared.Core.Injection;
using Hubcon.Shared.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Client.Builder
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconClientBuilder
    {
        private ProxyRegistry Proxies { get; }
        private ClientBuilderRegistry ClientBuilders { get; }

        private HubconClientBuilder()
        {
            Proxies = new ProxyRegistry();
            ClientBuilders = new ClientBuilderRegistry(Proxies);
        }

        private static HubconClientBuilder _current = null!;
        public static HubconClientBuilder Current
        {
            get
            {
                _current ??= new HubconClientBuilder();
                return _current;
            }
        }

        public IServiceCollection Services { get; internal set; }

        public IServiceCollection AddHubconClient(IServiceCollection services)
        {
            Services = services;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.WriteLine("UNHANDLED EXCEPTION:");
                Console.WriteLine(e.ExceptionObject);
                Console.WriteLine("Presione dos teclas para salir...");
                Console.ReadKey();
                Console.ReadKey();
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Console.WriteLine("UNOBSERVED TASK EXCEPTION:");
                Console.WriteLine(e.Exception);
                e.SetObserved();
            };

            services.AddSingleton<IProxyRegistry>(Proxies);
            services.AddSingleton<IClientBuilderRegistry>(ClientBuilders);
            services.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
            services.AddSingleton<IDynamicConverter, DynamicConverter>();
            services.AddTransient<IHubconClient, HubconClient>();

            var clientMappings = TransportTypeResolver.GetMappings();

            foreach (var mapping in clientMappings)
            {
                services.AddScoped(mapping.Value);
            }

            return services;
        }

        public IServiceCollection AddRemoteServerModule<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TRemoteServerModule>(IServiceCollection services, Func<TRemoteServerModule>? remoteServerFactory = null)
             where TRemoteServerModule : class, IRemoteServerModule
        {
            ClientBuilders.RegisterModule<TRemoteServerModule>(services, remoteServerFactory);
            return services;
        }

        public void LoadContractProxy(Type contractType, IServiceCollection services)
        {
            if (!typeof(IControllerContract).IsAssignableFrom(contractType))
                return;

            var proxy = GetProxyType(contractType);

            if (proxy == null)
                throw new InvalidOperationException($"No proxy found for contract type {contractType.FullName}. Ensure the proxy is defined and follows the naming convention.");

            Proxies.RegisterProxy(contractType, proxy);
            services.AddSingleton(proxy);
        }

        private static Func<Type, Type>? ProxyLookup;
        public static void SetupProxyLookup(Func<Type, Type> proxyLookup) => ProxyLookup = proxyLookup;

        public static Type? GetProxyType(Type contractType)
        {
            if(ProxyLookup != null)
                return ProxyLookup.Invoke(contractType);

            // Buscamos en todos los assemblies cargados
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Hubcon.Generated.ProxyLookup");
                if (type != null)
                {
                    var method = type.GetMethod("GetProxyType", BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        ProxyLookup = (x) => (Type)method.Invoke(null, new object[] { x });
                        return ProxyLookup.Invoke(contractType);
                    }
                }
            }

            return null;
        }


        public IServiceCollection UseContractsFromAssembly(IServiceCollection services, string assemblyName)
        {
            var assembly = AppDomain.CurrentDomain.Load(assemblyName);

            var contracts = assembly
                .GetTypes()
                .Where(t => t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t))
                .ToList();

            var proxies = assembly
                .GetTypes()
                .Where(t => !t.IsInterface && typeof(IControllerContract).IsAssignableFrom(t) && t.IsDefined(typeof(HubconProxyAttribute), inherit: true))
                .ToList();

            foreach (var contract in contracts)
            {
                var proxy = proxies.Find(x => x.Name == contract.Name + "Proxy")!;
                Proxies.RegisterProxy(contract, proxy);
                services.AddScoped(proxy);
            }

            return services;
        }
    }
}