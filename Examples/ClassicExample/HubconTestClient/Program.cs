using Hubcon.Client;
using HubconTestClient.Auth;
using HubconTestClient.Contracts;
using HubconTestClient.Models;
using HubconTestClient.Modules;
using HubconTestDomain;
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;

internal class Program
{
    private static int _finishedRequestsCount = 0;
    private static int _errors = 0;
    private static int _lastRequests = 0;
    private static int _maxReqs = 0;
    private static Stopwatch _sw;
    private static readonly ConcurrentBag<double> Latencies = new();

    static async Task Main()
    {
        Environment.SetEnvironmentVariable("HUBCON_CLIENT_CACHE_ENABLED", "true");

        Console.WriteLine($"¿Es Native AOT?: {System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported == false}");

        var process = Process.GetCurrentProcess();

        long coreMask = 0;

        int? customCores = 0;
        int cores = customCores ?? Environment.ProcessorCount - 1;

        for (int i = 0; i <= cores; i++)
        {
            coreMask |= 1L << i;
        }

        process.ProcessorAffinity = (IntPtr)coreMask;
        process.PriorityClass = ProcessPriorityClass.RealTime;

        var builder = WebApplication.CreateSlimBuilder();

        builder.Services.AddHubconClient();
        builder.Services.AddRemoteServerModule<TestModule>(() => new TestModule(new object()));
        builder.Services.AddRemoteServerModule<OpenAIServerModule>();

        builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        var app = builder.Build();
        var scope = app.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IUserContract>();
        var authManager = scope.ServiceProvider.GetRequiredService<AuthenticationManager>();
        var client2 = scope.ServiceProvider.GetRequiredService<ISecondTestContract>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IUserContract>>();


        var openAi = scope.ServiceProvider.GetRequiredService<IOpenAIContract>();

        //var command = new CreateResponseCommand()
        //{
        //    Model = "gpt-5-nano",
        //    Input = "Tell me a three sentence bedtime story about a unicorn."
        //};

        //var response = await openAi.CreateModelResponse(command);

        //var request = new OpenAIStreamRequest()
        //{
        //    Model = "gpt-5-nano",
        //    Input = "Hablame sobre las ultimas novedades de .NET 10 en un parrafo de aprox 500 palabras.",
        //};

        //var finalText = "";
        //await foreach(var item in openAi.GetResponseStream(request))
        //{
        //    logger.LogInformation($"Event received: {item.Delta}");
        //    finalText += item.Delta;
        //}

        //logger.LogInformation($"Final text: {finalText}");


        //var response2 = await openApi.GetModelResponseInputs(response.Id);

        //var response3 = await openApi.GetModelResponse(response.Id);

        //var response4 = await openApi.DeleteModelResponse(response3.Id);

        logger.LogInformation("Esperando interacción antes de iniciar las pruebas...");
        Console.ReadKey();

        await TestLogin(authManager, logger);
        await Task.Delay(100);


        //var response = await client.Execute(x => x.GetTemperatureFromServer("test"));

        //if (!response.Success || response.StatusCode != 200)
        //{
        //    // Hacer algo
        //}
        //else
        //{
        //    var data = response.Data;
        //    // Hago algo con data
        //}

        //await TestValidations(client, logger);
        //await Task.Delay(100);
        //await TestIngest(client, logger);
        //await Task.Delay(100);
        //await TestOverloading(client2, logger);
        //await Task.Delay(100);
        //await TestInvokeNoParameters(client2, logger);
        //await Task.Delay(100);
        //await TestSubscriptions(client, logger);
        //await Task.Delay(100);
        //await TestInvokeWithParameters(client, logger);
        //await Task.Delay(100);
        //await TestRemoteCancellation(client, logger);
        //await Task.Delay(100);
        //await TestSseStreaming(client, logger);
        //await Task.Delay(100);


        _sw = Stopwatch.StartNew();
        var ts = TimeSpan.FromSeconds(1);
        var worker = new System.Timers.Timer();
        int clientCount = 0;
        worker.Interval = 1000;
        worker.Elapsed += (sender, eventArgs) =>
        {
            var avgRequestsPerSec = _finishedRequestsCount - _lastRequests;

            double avgLatency = 0;
            double p50 = 0, p95 = 0, p99 = 0;

            var latenciesSnapshot = Latencies.ToArray();
            Latencies.Clear();

            if (latenciesSnapshot.Length > 0)
            {
                Array.Sort(latenciesSnapshot);
                avgLatency = latenciesSnapshot.Average();

                p50 = Percentile(latenciesSnapshot, 50);
                p95 = Percentile(latenciesSnapshot, 95);
                p99 = Percentile(latenciesSnapshot, 99);
            }

            _maxReqs = Math.Max(_maxReqs, avgRequestsPerSec);

            logger.LogInformation($" Client count: {clientCount} | Requests: {_finishedRequestsCount} | Avg requests/s: {avgRequestsPerSec} | Max req/s: {_maxReqs} | " +
                                  $"p50 latency(ms): {p50:F2} | p95 latency(ms): {p95:F2} | p99 latency(ms): {p99:F2} | Avg latency(ms): {avgLatency:F2}");

            var allocated = GC.GetTotalMemory(forceFullCollection: false);
            logger.LogInformation($"Heap Size: {allocated / 1024.0 / 1024.0:N2} MB - Time: {_sw.Elapsed}");

            _lastRequests = _finishedRequestsCount;
            _sw.Restart();
        };
        worker.Start();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 128
        };

        int rps = 9999999;

        await Parallel.ForEachAsync(Enumerable.Range(0, int.MaxValue), options, async (i, ct) =>
        {
            TokenBucketRateLimiter tokenBucketRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions()
            {
                QueueLimit = 1,
                AutoReplenishment = true,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokenLimit = rps,
                TokensPerPeriod = rps,
            });

            try
            {
                var paralellClient = scope.ServiceProvider.GetRequiredService<IUserContract>();
                Interlocked.Increment(ref clientCount);
                //await foreach(var item in client.GetMessages2())
                while (true)
                {
                    // var swReq = Stopwatch.StartNew();
                    //await tokenBucketRateLimiter.AcquireAsync();
                    //await client.IngestMessages(GetMessages2(), default);
                    //var item = await paralellClient.GetTemperatureFromServerWithInput(new TestInputClass(), ct);
                    //await paralellClient.Execute(x => x.ShowTextOnServer());
                    var item = await paralellClient.Execute(x => x.GetTemperatureFromServerWithInput(new TestInputClass(), ct));
                    //Interlocked.Increment(ref _finishedRequestsCount);
                }
            }
            finally
            {
                Interlocked.Decrement(ref clientCount);
            }
        });

        //int j = 0;
        //while (true)
        //{
        //    j++;
        //    // Un pequeño respiro cada N conexiones para no aturdir al SO
        //    if (j % 1000 == 0) Console.ReadKey();

        //    var paralellClient = scope.ServiceProvider.GetRequiredService<IUserContract>();
        //    Interlocked.Increment(ref clientCount);

        //    // var swReq = Stopwatch.StartNew();
        //    try
        //    {
        //        //await client.IngestMessages(GetMessages2(), default);
        //        var item = await paralellClient.GetTemperatureFromServerWithInput(new TestInputClass());
        //        Interlocked.Increment(ref _finishedRequestsCount);
        //    }
        //    catch (Exception ex)
        //    {
        //        Interlocked.Increment(ref _errors);
        //    }
        //    finally
        //    {
        //        // swReq.Stop();
        //        // Latencies.Add(swReq.Elapsed.TotalMilliseconds);
        //        //Interlocked.Decrement(ref clientCount);
        //    }
        //}
    }

    private static async Task TestSseStreaming(IUserContract client, ILogger<IUserContract> logger)
    {
        logger.LogInformation("Probando streaming por SSE, pidiendo 10 eventos...");

        var eventos = 0;
        var stream = await client.Execute(x => x.GetMessages(10));
        await foreach (var item in stream.Data!)
        {
            logger.LogInformation($"Evento recibido: {item}");
            eventos++;
        }

        if (eventos == 10)
            logger.LogInformation("SSE OK.");
        else
            throw new Exception("No se recibió la cantidad de eventos SSE pedida.");
    }

    private static async Task TestRemoteCancellation(IUserContract client, ILogger<IUserContract> logger)
    {
        logger.LogWarning("Probando cancelacion remota...");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        bool temp2 = false;
        try
        {
            var result = await client.Execute(x => x.GetTemperatureFromServerBlocking(cts.Token));
            temp2 = result.Data;
        }
        catch (Exception e)
        {
            if (e is OperationCanceledException)
                logger.LogInformation("Cancelacion OK.");
            else
            {
                logger.LogInformation($"FAILED: {e}");
            }
        }
    }

    private static async Task TestInvokeWithParameters(IUserContract client, ILogger<IUserContract> logger)
    {
        logger.LogWarning("Probando invocación con retorno...");

        var temp = await client.Execute(x => x.GetTemperatureFromServer(""));

        logger.LogInformation($"Invocación OK. Datos recibidos: {temp}");
    }

    private static async Task TestSubscriptions(IUserContract client, ILogger<IUserContract> logger)
    {
        int eventosRecibidos = 0;

        logger.LogWarning($"Comenzando prueba de suscripciones...");

        bool evento1 = false;

        async Task handler(int? input)
        {
            logger.LogInformation($"Evento recibido: {input}");
            Interlocked.Add(ref eventosRecibidos, 1);
            evento1 = true;
        }

        bool evento2 = false;
        async Task handler2(int? input)
        {
            logger.LogInformation($"Evento recibido: {input}");
            Interlocked.Add(ref eventosRecibidos, 1);
            evento2 = true;
        }

        bool evento3 = false;
        async Task handler3(int? input)
        {
            logger.LogInformation($"Evento recibido: {input}");
            Interlocked.Add(ref eventosRecibidos, 1);
            evento3 = true;
        }

        bool evento4 = false;
        async Task handler4(int? input)
        {
            logger.LogInformation($"Evento recibido: {input}");
            Interlocked.Add(ref eventosRecibidos, 1);
            evento4 = true;
        }

        async Task handler5(IEnumerable<int> input)
        {
            logger.LogInformation($"Evento recibido: [{string.Join(",", input)}]");
            Interlocked.Add(ref eventosRecibidos, 1);
            evento4 = true;
        }

        client.OnUserCreated!.AddHandler(handler);
        await client.OnUserCreated.Subscribe();
        client.OnUserCreated2!.AddHandler(handler2);
        await client.OnUserCreated2.Subscribe();
        client.OnUserCreated3!.AddHandler(handler3);
        await client.OnUserCreated3.Subscribe();
        client.OnUserCreated4!.AddHandler(handler4);
        await client.OnUserCreated4.Subscribe();
        client.OnEnumerableTest!.AddHandler(handler5);
        await client.OnEnumerableTest.Subscribe();

        logger.LogInformation("Eventos conectados.");

        await Task.Delay(100);

        logger.LogWarning("Enviando request de prueba...");
        var result = await client.Execute(x => x.CreateUser());
        logger.LogInformation($"Esperando eventos...");

        await Task.Delay(1000);

        if (eventosRecibidos == 5)
        {
            logger.LogInformation($"Eventos recibidos correctamente.");
        }
        else
        {
            throw new Exception("No se recibieron todos los eventos esperados.");
        }
    }

    private static async Task TestInvokeNoParameters(ISecondTestContract client2, ILogger<IUserContract> logger)
    {
        logger.LogWarning($"Probando invocación sin parametros...");
        var result = await client2.Execute(x => x.TestReturn());

        if (result.Success)
            logger.LogInformation($"Invocación sin parametros OK.");
        else
            throw new Exception("Invocación sin parametros fallida.");
    }

    private static async Task TestOverloading(ISecondTestContract client2, ILogger<IUserContract> logger)
    {
        logger.LogWarning($"Probando parametros sobrecargados sobre http...");
        try
        {
            var result1 = await client2.Execute(x => x.TestMethod());
            var result2 = await client2.Execute(x => x.TestMethod("test"));
        }
        catch (Exception ex)
        {
            logger.LogError($"Error en invocación sobrecargada: {ex}");
            logger.LogError($"La sobrecarga falló o no está habilitada.");
        }
    }

    private static async Task TestIngest(IUserContract client, ILogger<IUserContract> logger)
    {
        logger.LogWarning($"Probando ingest...");
        var source1 = GetMessages(3);
        var source2 = GetMessages(3);
        var source3 = GetMessages(3);
        var source4 = GetMessages(3);
        var source5 = GetMessages(3);
        var result = await client.Execute(x => x.IngestMessages2(source1, source2, source3, source4, source5));

        if (result.Failure)
            logger.LogInformation($"Ingest FAILED.");
        else
            logger.LogInformation($"Ingest OK.");
    }

    private static async Task TestValidations(IUserContract client, ILogger<IUserContract> logger)
    {
        var response = await client.Execute(x => x.IngestMessages(GetMessages(10), null));

        if (response.Failure)
            logger.LogInformation($"Validaciones OK.");
        else
            logger.LogInformation($"Error de validaciones: {response.Error}");
    }

    private static async Task TestLogin(AuthenticationManager authManager, ILogger<IUserContract> logger)
    {
        logger.LogWarning($"Probando login...");
        var result = await authManager.LoginAsync("miusuario", "");
        logger.LogInformation("{0}", $"Login result: {result.IsSuccess}");
        logger.LogInformation($"Login OK.");
    }

    static async IAsyncEnumerable<string> GetMessages(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("Enumerador cancelado.");
                break;
            }

            var message = $"string:{i}";
            Console.WriteLine($"Enviando mensaje... [{message}]");
            yield return message;
            await Task.Delay(1000);
        }
    }

    static async IAsyncEnumerable<string> GetMessages2()
    {
        //var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions()
        //{
        //    TokenLimit = 500000,
        //    TokensPerPeriod = 500000,
        //    ReplenishmentPeriod = TimeSpan.FromMilliseconds(100),
        //    AutoReplenishment = true,
        //    QueueLimit = 1000,
        //    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        //});

        while (true)
        {
            var swReq = Stopwatch.StartNew();
            try
            {
                yield return "hola";
                Interlocked.Increment(ref _finishedRequestsCount);
                //await limiter.AcquireAsync();
            }
            finally
            {
                swReq.Stop();
                Latencies.Add(swReq.Elapsed.TotalMilliseconds);
            }
        }
    }

    // Método auxiliar para calcular percentiles
    static double Percentile(double[] sortedData, double percentile)
    {
        if (sortedData == null || sortedData.Length == 0)
            return 0;

        double position = (percentile / 100.0) * (sortedData.Length + 1);
        int index = (int)position;

        if (index < 1) return sortedData[0];
        if (index >= sortedData.Length) return sortedData[^1];

        double fraction = position - index;
        return sortedData[index - 1] + fraction * (sortedData[index] - sortedData[index - 1]);
    }
}