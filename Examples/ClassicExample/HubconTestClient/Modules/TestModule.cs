using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using HubconTestClient.Auth;
using HubconTestDomain;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace HubconTestClient.Modules
{
    internal class TestModule(object item) : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("localhost:5000");

            configuration.EnableWebsocketAutoReconnect(true);

            configuration.GlobalLimit(2000000);

            //configuration.LimitIngest(100);
            //configuration.LimitSubscription(100);
            //configuration.LimitStreaming(100);
            //configuration.LimitWebsocketRoundTrip(100);
            //configuration.LimitHttpRoundTrip(100);
            //configuration.LimitWebsocketFireAndForget(100);
            //configuration.LimitHttpFireAndForget(100);

            //configuration.DisableAllLimiters();

            configuration.Implements<IUserContract>(contractConfigurator =>
            {
                contractConfigurator
                    .UseWebsocketMethods()
                    .AllowRemoteCancellation(false)
                    .AddHook(HookType.OnSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                    .ConfigureOperations(operationSelector =>
                    {
                        operationSelector.Configure(contract => contract.GetTemperatureFromServer)
                            .UseTransport(TransportType.Websockets)
                            .AddHook(HookType.OnSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                            .AddValidationHook(async ctx => 
                            { 
                                if (ctx.CancellationToken == CancellationToken.None) { int i = 0; /*Some operation*/ }                             
                            })
                            .LimitPerSecond(1000000);
                        
                        operationSelector.Configure(contract => contract.GetTemperatureFromServerBlocking)
                            .UseTransport(TransportType.Websockets)
                            .AddHook(HookType.OnSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                            .AddValidationHook(async ctx => 
                            { 
                                if (ctx.CancellationToken == CancellationToken.None) { int i = 0; /*Some operation*/ }                             
                            })
                            .AllowRemoteCancellation(false)
                            .LimitPerSecond(1000000);

                        operationSelector
                            .Configure(contract => contract.CreateUser)
                            .UseTransport(TransportType.Websockets)
                            .LimitPerSecond(1000000);
                    });
            });

            configuration.Implements<ISecondTestContract>();

            configuration.ConfigureWebsocketClient((x, services) =>
            {
                x.SetBuffer(4 * 1024, 4 * 1024);
            });

            configuration.SetWebsocketPingInterval(TimeSpan.FromSeconds(1));
            configuration.ScaleMessageProcessors(2);

            configuration.RequirePongResponse(true);

            configuration.ConfigureHttpClient((x, services) =>
            {
                x.Timeout = TimeSpan.FromSeconds(15);
                x.DefaultRequestHeaders.Add("User-Agent", "HubconTestClient");
            });

            // Manager de autenticación (opcional)
            configuration.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}