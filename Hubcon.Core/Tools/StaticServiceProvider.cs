using Hubcon.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Core.Tools
{
    public static class StaticServiceProvider
    {
        // Propiedad estática que accede al WebApplication configurado
        internal static IServiceProvider ServiceProvider
        {
            get
            {
                // Se asegura de que el _app ha sido inicializado antes de su uso
                if (_app == null)
                {
                    //throw new InvalidOperationException($"Use {nameof(DependencyInjection.UseHubcon)}() method after App.Build().");
                }
                return _app;
            }
            private set => _app = value;
        }

        private static IServiceProvider? _app;

        // Configura el WebApplication
        public static void Setup(WebApplication app) => _app = app.Services;
        public static void Setup(IServiceProvider app) => _app = app;
        public static void Setup(IServiceCollection serviceCollection, Action<IServiceCollection>? services = null)
        {
            // Se asegura de que el _app ha sido inicializado antes de su uso
            services?.Invoke(serviceCollection);
            _app = serviceCollection.BuildServiceProvider();
        }

        // Accede a los servicios del contenedor
        public static IServiceProvider Services
        {
            get
            {
                if (_app == null)
                {
                    //throw new InvalidOperationException($"Use {nameof(DependencyInjection.UseHubcon)}() method after App.Build().");
                }

                return ServiceProvider;
            }
        }
    }
}
