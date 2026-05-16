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
        public override void Configure(IServerModuleConfiguration server)
        {
            server.WithBaseUrl("http://localhost:5000");

            server.EnableWebsocketAutoReconnect(true);
            server.GlobalLimit(200000000);
            server.EnableLogging();
            server.DisableAllLimiters();
            server.AllowRemoteCancellation();
            server.RequirePongResponse(false);
            //server.AddHeaderProvider("key", x => "value");

            //server.DisableAllLimiters();

            server.Implements<IUserContract>(contractConfigurator =>
            {
                //contractConfigurator.AddHeaderProvider("key", x => "value2");
                contractConfigurator
                    .ForOperation(x => x.GetTemperatureFromServer(default!, default!))
                    .AddHook(HookType.OnSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                    .AddValidationHook(async ctx =>
                    {
                        if (ctx.CancellationToken == CancellationToken.None) { int i = 0; /*Some operation*/ }
                    });

                contractConfigurator
                    .AddHook(HookType.OnSend, async ctx => { })
                    .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnError, async ctx => { /*some error handling*/ });
            });

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

            server.UseAuthenticationManager<AuthenticationManager>(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5));

            server.UseInsecureConnection();
        }
    }
}