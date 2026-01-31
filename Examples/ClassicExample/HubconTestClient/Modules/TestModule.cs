using Hubcon;
using HubconTestClient.Auth;
using HubconTestDomain;
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
            server.WithBaseUrl("localhost:5000");

            server.EnableWebsocketAutoReconnect(true);
            server.GlobalLimit(200000000);
            server.EnableLogging();

            //configuration.LimitIngest(100);
            //configuration.LimitSubscription(100);
            //configuration.LimitStreaming(100);
            //configuration.LimitWebsocketRoundTrip(100);
            //configuration.LimitHttpRoundTrip(100);
            //configuration.LimitWebsocketFireAndForget(100);
            //configuration.LimitHttpFireAndForget(100);

            server.DisableAllLimiters();

            server.Implements<IUserContract>((contractConfigurator =>
            {
                contractConfigurator
                    .SetDefaultTransport<WebSocketTransport>()
                    .AllowRemoteCancellation(false)
                    //.AddHook(HookType.OnSend, async ctx => ctx.Services
                    //    .GetRequiredService<ILogger<object>>()
                    //    .LogInformation($"Operation {ctx.Request.OperationName} called. OnSend hook working."))
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
                            })
                            .LimitPerSecond(100);

                        operationSelector.Configure(contract => contract.GetTemperatureFromServerBlocking(default))
                            .LimitPerSecond(10000000);

                        operationSelector
                            .Configure(contract => contract.CreateUser(default))
                            .LimitPerSecond(1000000);

                        //operationSelector
                        //    .Configure(contract => contract.OnUserCreated)
                        //    //.AddHook(HookType.OnEventReceived, async ctx => {  /*some operation logging or notification*/ })
                        //    .LimitPerSecond(1000000);

                        operationSelector
                            .Configure(contract => contract.GetTemperatureFromServerWithInput(default, default))
                            .UseTransport<WebSocketTransport>();

                        operationSelector
                            .Configure(contract => contract.GetMessages(default(int)))
                            .UseTransport<HttpTransport>();
                    }));
            }));

            server.Implements<ISecondTestContract>(contractConfigurator =>
            {
                contractConfigurator.ConfigureOperations(operationSelector =>
                {
                    operationSelector.Configure(c => c.TestMethod(default!)).UseTransport<HttpTransport>();
                });
            });

            server.ConfigureWebsocketClient((x, services) =>
            {
                x.SetBuffer(4 * 1024, 4 * 1024);
                x.SetRequestHeader("Origin", "Hubcon");
            });

            server.SetWebsocketPingInterval(TimeSpan.FromSeconds(15));
            server.ScaleMessageProcessors(4);

            server.ConfigureHttpClient((options, services) =>
            {
                options.Timeout = TimeSpan.FromSeconds(15);
                options.DefaultRequestHeaders.Add("Origin", "Hubcon");
            });


            server.UseAuthenticationManager<AuthenticationManager>();

            server.AddInterceptor(InterceptorType.OnPing, async ctx =>
            {
                var authManager = (AuthenticationManager)ctx.Services.GetService(typeof(AuthenticationManager));
                var logger = (ILogger<object>)ctx.Services.GetService(typeof(ILogger<object>));

                var currentTime = DateTimeOffset.UtcNow.DateTime;
                var lowerTime = authManager.AccessTokenExpiresAt.HasValue ? authManager.AccessTokenExpiresAt.Value.AddMinutes(-1) : DateTime.MaxValue;

                if (currentTime > lowerTime)
                {
                    IHubconResult? refreshedToken = null!;
                    try
                    {
                        refreshedToken = await authManager.TryRefreshSessionAsync();
                        await ctx.TryRefreshToken.Invoke(authManager.AccessToken!);
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