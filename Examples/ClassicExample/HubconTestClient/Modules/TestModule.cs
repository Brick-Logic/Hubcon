using Hubcon;
using HubconTestClient.Auth;
using HubconTestDomain;

namespace HubconTestClient.Modules
{
    internal class TestModule(object item) : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("localhost:5000");

            configuration.EnableWebsocketAutoReconnect(true);
            configuration.GlobalLimit(200000000);
            configuration.EnableLogging();

            //configuration.LimitIngest(100);
            //configuration.LimitSubscription(100);
            //configuration.LimitStreaming(100);
            //configuration.LimitWebsocketRoundTrip(100);
            //configuration.LimitHttpRoundTrip(100);
            //configuration.LimitWebsocketFireAndForget(100);
            //configuration.LimitHttpFireAndForget(100);

            configuration.DisableAllLimiters();

            configuration.Implements<IUserContract>(contractConfigurator =>
            {
                contractConfigurator
                    .UseWebsocketMethods()
                    .AllowRemoteCancellation(false)
                    //.AddHook(HookType.OnSend, async ctx => ctx.Services
                    //    .GetRequiredService<ILogger<object>>()
                    //    .LogInformation($"Operation {ctx.Request.OperationName} called. OnSend hook working."))
                    .AddHook(HookType.OnSend, async ctx => { })
                    .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                    .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                    .ConfigureOperations(operationSelector =>
                    {
                        operationSelector.Configure(contract => contract.GetTemperatureFromServer)
                            .AddHook(HookType.OnSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnAfterSend, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnResponse, async ctx => { /*some operation logging or notification*/ })
                            .AddHook(HookType.OnError, async ctx => { /*some error handling*/ })
                            .AddValidationHook(async ctx =>
                            {
                                if (ctx.CancellationToken == CancellationToken.None) { int i = 0; /*Some operation*/ }
                            })
                            .LimitPerSecond(100);

                        operationSelector.Configure(contract => contract.GetTemperatureFromServerBlocking)
                            .LimitPerSecond(10000000);

                        operationSelector
                            .Configure(contract => contract.CreateUser)
                            .LimitPerSecond(1000000);

                        //operationSelector
                        //    .Configure(contract => contract.OnUserCreated)
                        //    //.AddHook(HookType.OnEventReceived, async ctx => {  /*some operation logging or notification*/ })
                        //    .LimitPerSecond(1000000);

                        operationSelector
                            .Configure(contract => contract.GetTemperatureFromServerWithInput)
                            .UseTransport(TransportType.Websockets);

                        operationSelector
                            .Configure(contract => contract.GetMessages(default(int)))
                            .UseTransport(TransportType.Http);
                    });
            });

            configuration.Implements<ISecondTestContract>(contractConfigurator =>
            {
                contractConfigurator.ConfigureOperations(operationSelector =>
                {
                    operationSelector.Configure(c => c.TestMethod(default!)).UseTransport(TransportType.Http);
                });
            });

            configuration.ConfigureWebsocketClient((x, services) =>
            {
                x.SetBuffer(4 * 1024, 4 * 1024);
                x.SetRequestHeader("Origin", "Hubcon");
            });

            configuration.SetWebsocketPingInterval(TimeSpan.FromSeconds(15));
            configuration.ScaleMessageProcessors(4);

            configuration.ConfigureHttpClient((options, services) =>
            {
                options.Timeout = TimeSpan.FromSeconds(15);
                options.DefaultRequestHeaders.Add("Origin", "Hubcon");
            });


            configuration.UseAuthenticationManager<AuthenticationManager>();

            configuration.AddInterceptor(InterceptorType.OnPing, async ctx =>
            {
                var authManager = ctx.Services.GetRequiredService<AuthenticationManager>();
                var logger = ctx.Services.GetRequiredService<ILogger<object>>();

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

            configuration.UseInsecureConnection();
        }
    }
}