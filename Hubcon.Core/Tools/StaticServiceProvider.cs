using Hubcon.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Core.Tools
{
    public static class StaticServiceProvider
    {
        // Propiedad estática que accede al WebApplication configurado
        internal static WebApplication App
        {
            get
            {
                // Se asegura de que el _app ha sido inicializado antes de su uso
                if (_app == null)
                {
                    throw new InvalidOperationException($"Use {nameof(DependencyInjection.UseHubcon)}() method after App.Build().");
                }
                return _app;
            }
            private set => _app = value;
        }

        private static WebApplication? _app;

        // Configura el WebApplication
        internal static void Setup(WebApplication app) => _app = app;

        // Accede a los servicios del contenedor
        public static IServiceProvider Services
        {
            get
            {
                if (_app == null)
                {
                    throw new InvalidOperationException($"Use {nameof(DependencyInjection.UseHubcon)}() method after App.Build().");
                }

                return App.Services;
            }
        }
    }
}
