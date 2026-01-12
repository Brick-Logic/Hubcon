//using Hubcon.Client;
//using HubconTestClient.Auth;
//using HubconTestClient.Modules;
//using HubconTestDomain;
//using System.Collections.Concurrent;
//using System.Diagnostics;
//using System.Runtime.CompilerServices;
//using System.Threading.RateLimiting;

//internal class Program
//{
//    private static int _finishedRequestsCount = 0;
//    private static int _errors = 0;
//    private static int _lastRequests = 0;
//    private static int _maxReqs = 0;
//    private static Stopwatch _sw;
//    private static readonly ConcurrentBag<double> Latencies = new();

//    static async Task Main()
//    {
//        Environment.SetEnvironmentVariable("HUBCON_CLIENT_CACHE_ENABLED", "true");

//        var process = Process.GetCurrentProcess();

//        long coreMask = 0;

//        int? customCores = 3;
//        int cores = customCores ?? Environment.ProcessorCount - 1;

//        for (int i = 0; i <= cores; i++)
//        {
//            coreMask |= 1L << i;
//        }

//        process.ProcessorAffinity = (IntPtr)coreMask;
//        process.PriorityClass = ProcessPriorityClass.RealTime;

//        var builder = WebApplication.CreateBuilder();

//        builder.Services.AddHubconClient();
//        builder.Services.AddRemoteServerModule<TestModule>(() => new TestModule(new object()));
//        builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
//        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

//        var app = builder.Build();
//        var scope = app.Services.CreateScope();

//        var client = scope.ServiceProvider.GetRequiredService<IUserContract>();
//        var authManager = scope.ServiceProvider.GetRequiredService<AuthenticationManager>();
//        var client2 = scope.ServiceProvider.GetRequiredService<ISecondTestContract>();
//        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IUserContract>>();


//        logger.LogInformation("Esperando interacción antes de iniciar las pruebas...");

//        Console.ReadKey();

//        logger.LogWarning($"Probando login...");
//        var result = await authManager.LoginAsync("miusuario", "");
//        logger.LogInformation("{0}", $"Login result: {result.IsSuccess}");
//        logger.LogInformation($"Login OK.");

//        await Task.Delay(100);


//        try
//        {
//            await client.IngestMessages(GetMessages(10), null);
//        }
//        catch (Exception ex)
//        {
//            logger.LogInformation($"Validaciones OK.");
//        }

//        await Task.Delay(100);

//        logger.LogWarning($"Probando ingest...");
//        var source1 = GetMessages(3);
//        var source2 = GetMessages(3);
//        var source3 = GetMessages(3);
//        var source4 = GetMessages(3);
//        var source5 = GetMessages(3);
//        await client.IngestMessages2(source1, source2, source3, source4, source5);
//        logger.LogInformation($"Ingest OK.");

//        await Task.Delay(1000);

//        logger.LogWarning($"Probando invocación sin parametros...");
//        var text = await client2.TestReturn();

//        logger.LogWarning($"Probando parametros sobrecargados sobre http...");
//        try
//        {
//            await client2.TestMethod();
//            await client2.TestMethod("hola");
//        }
//        catch (Exception ex)
//        {
//            logger.LogError($"Error en invocación sobrecargada: {ex}");
//            logger.LogError($"La sobrecarga falló o no está habilitada.");
//        }


//        if (text != null)
//            logger.LogInformation($"Invocación sin parametros OK.");
//        else
//            throw new Exception("Invocación sin parametros fallida.");

//        await Task.Delay(100);

//        int eventosRecibidos = 0;

//        logger.LogWarning($"Comenzando prueba de suscripciones...");

//        bool evento1 = false;

//        async Task handler(int? input)
//        {
//            logger.LogInformation($"Evento recibido: {input}");
//            Interlocked.Add(ref eventosRecibidos, 1);
//            evento1 = true;
//        }

//        bool evento2 = false;
//        async Task handler2(int? input)
//        {
//            logger.LogInformation($"Evento recibido: {input}");
//            Interlocked.Add(ref eventosRecibidos, 1);
//            evento2 = true;
//        }

//        bool evento3 = false;
//        async Task handler3(int? input)
//        {
//            logger.LogInformation($"Evento recibido: {input}");
//            Interlocked.Add(ref eventosRecibidos, 1);
//            evento3 = true;
//        }

//        bool evento4 = false;
//        async Task handler4(int? input)
//        {
//            logger.LogInformation($"Evento recibido: {input}");
//            Interlocked.Add(ref eventosRecibidos, 1);
//            evento4 = true;
//        }

//        //client.OnUserCreated!.AddHandler(handler);
//        //await client.OnUserCreated.Subscribe();
//        //client.OnUserCreated2!.AddHandler(handler2);
//        //await client.OnUserCreated2.Subscribe();
//        //client.OnUserCreated3!.AddHandler(handler3);
//        //await client.OnUserCreated3.Subscribe();
//        //client.OnUserCreated4!.AddHandler(handler4);
//        //await client.OnUserCreated4.Subscribe();

//        logger.LogInformation("Eventos conectados.");

//        await Task.Delay(100);

//        logger.LogWarning("Enviando request de prueba...");
//        await client.CreateUser();
//        logger.LogInformation($"Esperando eventos...");

//        //await Task.Delay(3000);

//        //if (eventosRecibidos == 4)
//        //{
//        //    logger.LogInformation($"Eventos recibidos correctamente.");
//        //}
//        //else
//        //{
//        //    throw new Exception("No se recibieron todos los eventos esperados.");
//        //}

//        await Task.Delay(100);

//        logger.LogWarning("Probando invocación con retorno...");

//        var temp = await client.GetTemperatureFromServer("");

//        logger.LogInformation($"Invocación OK. Datos recibidos: {temp}");

//        await Task.Delay(100);

//        logger.LogWarning("Probando cancelacion remota...");
//        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
//        bool temp2 = false;
//        try
//        {
//            temp2 = await client.GetTemperatureFromServerBlocking(cts.Token);
//        }
//        catch (Exception e)
//        {
//            logger.LogInformation(e.ToString());
//        }

//        logger.LogInformation($"Cancelacion OK. Datos recibidos: {temp2}");

//        await Task.Delay(100);

//        logger.LogWarning("Probando streaming de 10 mensajes...");

//        await foreach (var item in client.GetMessages(10))
//        {
//            logger.LogInformation($"Respuesta recibida: {item}");
//        }

//        logger.LogInformation("Streaming OK.");

//        await Task.Delay(100);

//        _sw = Stopwatch.StartNew();
//        var ts = TimeSpan.FromSeconds(1);
//        var worker = new System.Timers.Timer();
//        int clientCount = 0;
//        worker.Interval = 1000;
//        worker.Elapsed += (sender, eventArgs) =>
//        {
//            var avgRequestsPerSec = _finishedRequestsCount - _lastRequests;

//            double avgLatency = 0;
//            double p50 = 0, p95 = 0, p99 = 0;

//            var latenciesSnapshot = Latencies.ToArray();
//            Latencies.Clear();

//            if (latenciesSnapshot.Length > 0)
//            {
//                Array.Sort(latenciesSnapshot);
//                avgLatency = latenciesSnapshot.Average();

//                p50 = Percentile(latenciesSnapshot, 50);
//                p95 = Percentile(latenciesSnapshot, 95);
//                p99 = Percentile(latenciesSnapshot, 99);
//            }

//            _maxReqs = Math.Max(_maxReqs, avgRequestsPerSec);

//            logger.LogInformation($" Client count: {clientCount} | Requests: {_finishedRequestsCount} | Avg requests/s: {avgRequestsPerSec} | Max req/s: {_maxReqs} | " +
//                                  $"p50 latency(ms): {p50:F2} | p95 latency(ms): {p95:F2} | p99 latency(ms): {p99:F2} | Avg latency(ms): {avgLatency:F2}");

//            var allocated = GC.GetTotalMemory(forceFullCollection: false);
//            logger.LogInformation($"Heap Size: {allocated / 1024.0 / 1024.0:N2} MB - Time: {_sw.Elapsed}");

//            _lastRequests = _finishedRequestsCount;
//            _sw.Restart();
//        };
//        worker.Start();

//        var options = new ParallelOptions
//        {
//            MaxDegreeOfParallelism = 4
//        };

//        int rps = 9999999;

//        await Parallel.ForEachAsync(Enumerable.Range(0, int.MaxValue), options, async (i, ct) =>
//        {
//            TokenBucketRateLimiter tokenBucketRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions()
//            {
//                QueueLimit = 1,
//                AutoReplenishment = true,
//                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
//                TokenLimit = rps,
//                TokensPerPeriod = rps,
//            });

//            try
//            {
//                var paralellClient = scope.ServiceProvider.GetRequiredService<IUserContract>();
//                Interlocked.Increment(ref clientCount);
//                await foreach (var item in client.GetMessages2())
//                //while (true)
//                {
//                    // var swReq = Stopwatch.StartNew();
//                    try
//                    {
//                        //await tokenBucketRateLimiter.AcquireAsync();
//                        //await client.IngestMessages(GetMessages2(), default);
//                        //var item = await paralellClient.GetTemperatureFromServerWithInput(new TestInputClass(), ct);
//                        Interlocked.Increment(ref _finishedRequestsCount);
//                    }
//                    catch (Exception ex)
//                    {
//                        Interlocked.Increment(ref _errors);
//                    }
//                    finally
//                    {
//                        // swReq.Stop();
//                        // Latencies.Add(swReq.Elapsed.TotalMilliseconds);
//                    }
//                }
//            }
//            finally
//            {
//                Interlocked.Decrement(ref clientCount);
//            }
//        });
//    }

//    static async IAsyncEnumerable<string> GetMessages(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
//    {
//        for (int i = 0; i < count; i++)
//        {
//            if (cancellationToken.IsCancellationRequested)
//            {
//                Console.WriteLine("Enumerador cancelado.");
//                break;
//            }

//            var message = $"string:{i}";
//            Console.WriteLine($"Enviando mensaje... [{message}]");
//            yield return message;
//            await Task.Delay(1000);
//        }
//    }

//    static async IAsyncEnumerable<string> GetMessages2()
//    {
//        //var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions()
//        //{
//        //    TokenLimit = 500000,
//        //    TokensPerPeriod = 500000,
//        //    ReplenishmentPeriod = TimeSpan.FromMilliseconds(100),
//        //    AutoReplenishment = true,
//        //    QueueLimit = 1000,
//        //    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
//        //});

//        while (true)
//        {
//            var swReq = Stopwatch.StartNew();
//            try
//            {
//                yield return "hola";
//                Interlocked.Increment(ref _finishedRequestsCount);
//                //await limiter.AcquireAsync();
//            }
//            finally
//            {
//                swReq.Stop();
//                Latencies.Add(swReq.Elapsed.TotalMilliseconds);
//            }
//        }
//    }

//    // Método auxiliar para calcular percentiles
//    static double Percentile(double[] sortedData, double percentile)
//    {
//        if (sortedData == null || sortedData.Length == 0)
//            return 0;

//        double position = (percentile / 100.0) * (sortedData.Length + 1);
//        int index = (int)position;

//        if (index < 1) return sortedData[0];
//        if (index >= sortedData.Length) return sortedData[^1];

//        double fraction = position - index;
//        return sortedData[index - 1] + fraction * (sortedData[index] - sortedData[index - 1]);
//    }
//}







using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
//using System.Net.NetworkInformation;
//using System.Reflection;
//using System.Text.Json;

//ClientBuilderRegistry.cs(26): Trim analysis warning IL2091: Hubcon.Client.Builder.ClientBuilderRegistry.RegisterModule<TRemoteServerModule>(IServiceCollection, Func`1 < !!0 >): 'T' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in 'System.Activator.CreateInstance<T>()'. The generic parameter 'TRemoteServerModule' of 'Hubcon.Client.Builder.ClientBuilderRegistry.RegisterModule<TRemoteServerModule>(IServiceCollection,Func`1<!!0>)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    ILC : Trim analysis warning IL2091: Hubcon.Shared.Core.Injection.LazyResolver`1: 'T' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in 'System.Lazy`1'. The generic parameter 'T' of 'Hubcon.Shared.Core.Injection.LazyResolver`1' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    LazyResolver.cs(10): Trim analysis warning IL2091: Hubcon.Shared.Core.Injection.LazyResolver`1.LazyResolver`1(IServiceProvider): 'T' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in 'System.Lazy`1'. The generic parameter 'T' of 'Hubcon.Shared.Core.Injection.LazyResolver`1' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    ClientBuilder.cs(204): Trim analysis warning IL2070: Hubcon.Client.Builder.ClientBuilder.GetOrCreateClient(Type, IServiceProvider, Boolean): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to 'System.Type.GetProperties()'. The parameter 'contractType' of method 'Hubcon.Client.Builder.ClientBuilder.GetOrCreateClient(Type,IServiceProvider,Boolean)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    ClientBuilder.cs(193): Trim analysis warning IL2070: Hubcon.Client.Builder.ClientBuilder.<> c.< GetOrCreateClient > b__163_1(Type): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to 'System.Type.GetProperties()'. The parameter 'x' of method 'Hubcon.Client.Builder.ClientBuilder.<>c.<GetOrCreateClient>b__163_1(Type)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    ClientBuilder.cs(202): AOT analysis warning IL3050: Hubcon.Client.Builder.ClientBuilder.<> c.< GetOrCreateClient > b__163_3(Type): Using member 'System.Type.MakeGenericType(Type[])' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. The native code for this instantiation might not be available at runtime.
//    ClientBuilder.cs(272): Trim analysis warning IL2091: Hubcon.Client.Builder.ClientBuilder.UseAuthenticationManager<T>(IServiceCollection): 'TService' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in 'Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<TService>(IServiceCollection)'. The generic parameter 'T' of 'Hubcon.Client.Builder.ClientBuilder.UseAuthenticationManager<T>(IServiceCollection)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    ProxyRegistry.cs(67): Trim analysis warning IL2026: Hubcon.Client.Core.Registries.ProxyRegistry.TryGetProxy(Type): Using member 'System.Reflection.Assembly.GetTypes()' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Types might be removed.
//    HubconClient.cs(739): AOT analysis warning IL3050: Hubcon.Client.Integration.Client.HubconClient.Build(IClientOptions, IServiceProvider, IDictionary`2 < Type, IContractOptions >, Boolean): Using member 'System.Type.MakeGenericType(Type[])' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. The native code for this instantiation might not be available at runtime.
//    HubconClient.cs(739): Trim analysis warning IL2076: Hubcon.Client.Integration.Client.HubconClient.Build(IClientOptions, IServiceProvider, IDictionary`2 < Type, IContractOptions >, Boolean): 'T' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in 'System.Lazy`1'. The return value of method 'Hubcon.Client.Abstractions.Interfaces.IClientOptions.AuthenticationManagerType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    HubconClient.cs(740): Trim analysis warning IL2026: Hubcon.Client.Integration.Client.HubconClient.<> c__DisplayClass35_1.< Build > b__1(): Using member 'Microsoft.CSharp.RuntimeBinder.Binder.Convert(CSharpBinderFlags,Type,Type)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Using dynamic types might cause types or members to be removed by trimmer.
//    HubconClient.cs(740): AOT analysis warning IL3050: Hubcon.Client.Integration.Client.HubconClient.<> c__DisplayClass35_1.< Build > b__1(): Using member 'System.Runtime.CompilerServices.CallSite`1<Func`3<CallSite,Object,IAuthenticationManager>>.Create(CallSiteBinder)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. Creating arrays at runtime requires dynamic code generation.
//    HubconClient.cs(740): Trim analysis warning IL2026: Hubcon.Client.Integration.Client.HubconClient.<> c__DisplayClass35_1.< Build > b__1(): Using member 'Microsoft.CSharp.RuntimeBinder.Binder.GetMember(CSharpBinderFlags,String,Type,IEnumerable`1<CSharpArgumentInfo>)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Using dynamic types might cause types or members to be removed by trimmer.
//    HubconClient.cs(740): AOT analysis warning IL3050: Hubcon.Client.Integration.Client.HubconClient.<> c__DisplayClass35_1.< Build > b__1(): Using member 'System.Runtime.CompilerServices.CallSite`1<Func`3<CallSite,Object,Object>>.Create(CallSiteBinder)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. Creating arrays at runtime requires dynamic code generation.
//    ClientBuilder.cs(246): Trim analysis warning IL2026: Hubcon.Client.Builder.ClientBuilder.GetFactory(): Using member 'System.Reflection.Assembly.GetType(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Types might be removed by trimming. If the type name is a string literal, consider using Type.GetType instead.
//    ClientBuilder.cs(249): Trim analysis warning IL2075: Hubcon.Client.Builder.ClientBuilder.GetFactory(): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicMethods' in call to 'System.Type.GetMethod(String,BindingFlags)'. The return value of method 'System.Reflection.Assembly.GetType(String)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    PropertyTools.cs(19): Trim analysis warning IL2075: Hubcon.Shared.Core.Tools.PropertyTools.AssignProperty<T>(T, PropertyInfo, Object): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields', 'DynamicallyAccessedMemberTypes.NonPublicFields' in call to 'System.Type.GetField(String,BindingFlags)'. The return value of method 'System.Reflection.MemberInfo.DeclaringType.get' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    PropertyTools.cs(32): Trim analysis warning IL2075: Hubcon.Shared.Core.Tools.PropertyTools.AssignProperty(Object, String, Object): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields', 'DynamicallyAccessedMemberTypes.NonPublicFields' in call to 'System.Type.GetField(String,BindingFlags)'. The return value of method 'System.Object.GetType()' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    HubconClientBuilder.cs(75): Trim analysis warning IL2072: Hubcon.Client.Builder.HubconClientBuilder.LoadContractProxy(Type, IServiceCollection): 'serviceType' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to 'Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(IServiceCollection,Type)'. The return value of method 'Hubcon.Client.Builder.HubconClientBuilder.GetProxyType(Type)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    HubconClientBuilder.cs(84): Trim analysis warning IL2026: Hubcon.Client.Builder.HubconClientBuilder.GetProxyType(Type): Using member 'System.Reflection.Assembly.GetType(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Types might be removed by trimming. If the type name is a string literal, consider using Type.GetType instead.
//    HubconClientBuilder.cs(91): Trim analysis warning IL2026: Hubcon.Client.Builder.HubconClientBuilder.GetProxyType(Type): Using member 'System.Reflection.Assembly.GetType(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Types might be removed by trimming. If the type name is a string literal, consider using Type.GetType instead.
//    runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.11\framework\Microsoft.CSharp.dll : warning IL3053: Assembly 'Microsoft.CSharp' produced AOT analysis warnings.
//    DynamicConverter.cs(212): Trim analysis warning IL2026: Hubcon.Shared.Core.Serialization.DynamicConverter.DeserializeJsonElement<T>(JsonElement): Using member 'System.Text.Json.JsonSerializer.Deserialize<T>(JsonElement,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
//    DynamicConverter.cs(212): AOT analysis warning IL3050: Hubcon.Shared.Core.Serialization.DynamicConverter.DeserializeJsonElement<T>(JsonElement): Using member 'System.Text.Json.JsonSerializer.Deserialize<T>(JsonElement,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.
//    OperationSelector.cs(162): Trim analysis warning IL2075: Hubcon.Client.Core.Configurations.GlobalOperationConfigurator`1.ExtractMethodFromConstantValue(Object): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields', 'DynamicallyAccessedMemberTypes.NonPublicFields' in call to 'System.Type.GetFields(BindingFlags)'. The return value of method 'System.Object.GetType()' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    .nuget\packages\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.11\framework\System.Linq.Expressions.dll : warning IL3053: Assembly 'System.Linq.Expressions' produced AOT analysis warnings.
//    ClientBuilder.cs(299): AOT analysis warning IL3050: Hubcon.Client.Builder.ClientBuilder.<>c.<GetContractOptions>b__172_0(Type): Using member 'System.Type.MakeGenericType(Type[])' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. The native code for this instantiation might not be available at runtime.
//    DynamicConverter.cs(337): Trim analysis warning IL2026: Hubcon.Shared.Core.Serialization.HubconJsonDefaults.TryGetGeneratedOptions(): Using member 'System.Reflection.Assembly.GetType(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. Types might be removed by trimming. If the type name is a string literal, consider using Type.GetType instead.
//    DynamicConverter.cs(340): Trim analysis warning IL2075: Hubcon.Shared.Core.Serialization.HubconJsonDefaults.TryGetGeneratedOptions(): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields' in call to 'System.Type.GetField(String,BindingFlags)'. The return value of method 'System.Reflection.Assembly.GetType(String)' does not have matching annotations. The source value must declare at least the same requirements as those declared on the target location it is assigned to.
//    DynamicConverter.cs(357): AOT analysis warning IL3050: Hubcon.Shared.Core.Serialization.HubconJsonDefaults.CreateFallbackOptions(): Using member 'System.Text.Json.Serialization.JsonStringEnumConverter.JsonStringEnumConverter()' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JsonStringEnumConverter cannot be statically analyzed and requires runtime code generation. Applications should use the generic JsonStringEnumConverter<TEnum> instead.
//    DynamicConverter.cs(290): Trim analysis warning IL2026: Hubcon.Shared.Core.Serialization.DynamicConverter.SerializeToElement<T>(!!0): Using member 'System.Text.Json.JsonSerializer.SerializeToElement<T>(T,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
//    DynamicConverter.cs(290): AOT analysis warning IL3050: Hubcon.Shared.Core.Serialization.DynamicConverter.SerializeToElement<T>(!!0): Using member 'System.Text.Json.JsonSerializer.SerializeToElement<T>(T,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.
//    HubconWebsocketClient.cs(895): Trim analysis warning IL2026: Hubcon.Client.Core.Websockets.HubconWebSocketClient.<SendMessageAsync>d__62`1.MoveNext(): Using member 'System.Text.Json.JsonSerializer.Serialize<T>(Utf8JsonWriter,T,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
//    HubconWebsocketClient.cs(895): AOT analysis warning IL3050: Hubcon.Client.Core.Websockets.HubconWebSocketClient.<SendMessageAsync>d__62`1.MoveNext(): Using member 'System.Text.Json.JsonSerializer.Serialize<T>(Utf8JsonWriter,T,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.
//    DynamicConverter.cs(146): Trim analysis warning IL2026: Hubcon.Shared.Core.Serialization.DynamicConverter.DeserializeData<T>(Object): Using member 'System.Text.Json.JsonSerializer.Deserialize<T>(JsonElement,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
//    DynamicConverter.cs(146): AOT analysis warning IL3050: Hubcon.Shared.Core.Serialization.DynamicConverter.DeserializeData<T>(Object): Using member 'System.Text.Json.JsonSerializer.Deserialize<T>(JsonElement,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.
//    DynamicConverter.cs(154): Trim analysis warning IL2026: Hubcon.Shared.Core.Serialization.DynamicConverter.DeserializeData<T>(Object): Using member 'System.Text.Json.JsonSerializer.Deserialize<T>(String,JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
//    DynamicConverter.cs(154): AOT analysis warning IL3050: Hubcon.Shared.Core.Serialization.DynamicConverter.DeserializeData<T>(Object): Using member 'System.Text.Json.JsonSerializer.Deserialize<T>(String,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.














