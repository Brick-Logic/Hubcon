using Hubcon.Blazor.Client.Modules;
using Hubcon.Blazor.Components;
using Hubcon.Client;

namespace Hubcon.Blazor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveWebAssemblyComponents();


            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<TestModule>();
            builder.Services.AddSingleton<HttpClient, HubconHttpClient>();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .AllowAnyOrigin()    // Permite cualquier origen (¡útil para desarrollo!)
                        .AllowAnyMethod()    // Permite GET, POST, PUT, DELETE, etc.
                        .AllowAnyHeader();   // Permite cualquier encabezado
                });
            });

            var app = builder.Build();

            app.UseCors();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

            app.Run();
        }
    }
}
