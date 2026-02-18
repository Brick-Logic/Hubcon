using Hubcon;
using HubconTestClient.Auth;
using HubconTestDomain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace HubconTestClient.Modules
{
    internal class TestModule : RemoteServerModule
    {
        public TestModule(object item)
        {
        }

        public override void Configure(IServerModuleConfiguration server)
        {
            server.WithBaseUrl("http://localhost:5000");

            server.EnableWebsocketAutoReconnect(true);
            server.GlobalLimit(200000000);
            server.EnableLogging();

            server.AddHeaderProvider("key", x => "value");

            server.DisableAllLimiters();

            server.Implements<IUserContract>((contractConfigurator =>
            {
                contractConfigurator.AddHeaderProvider("key", x => "value");

                contractConfigurator
                    .AllowRemoteCancellation(false)
                    .AddHook(HookType.OnSend, async ctx => { })
                    .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                    .ConfigureOperations((operationSelector =>
                    {
                        operationSelector.Configure(contract => contract.GetTemperatureFromServer(default, default))
                            .AddHook(HookType.OnSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                            .AddValidationHook(async ctx =>
                            {
                                if (ctx.CancellationToken == CancellationToken.None) { int i = 0; /*Some operation*/ }
                            });
                    }));
            }));

            server.Implements<ISecondTestContract>();

            server.ConfigureWebsocketClient((x, services) =>
            {
                x.SetBuffer(4 * 1024, 4 * 1024);
                x.SetRequestHeader("Origin", "Hubcon");
            });

            server.ConfigureHttpClient((options, services) =>
            {
                options.Timeout = TimeSpan.FromSeconds(15);
                options.DefaultRequestHeaders.Add("Origin", "Hubcon");
            });

            server.UseAuthenticationManager<AuthenticationManager>();

            server.AddInterceptor(InterceptorType.OnPing, async ctx =>
            {
                var authManager = ctx.Services.GetRequiredService<AuthenticationManager>();
                var logger = ctx.Services.GetRequiredService<ILogger<object>>();

                if (authManager.ShouldRefreshSession)
                {
                    IHubconResult? refreshedToken = null!;
                    try
                    {
                        refreshedToken = await authManager.TryRefreshSessionAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Token refresh error: {Message}.", ex.Message);
                    }
                }
            });

            server.UseInsecureConnection();
        }
    }
}