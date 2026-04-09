using Hubcon;
using HubconTestClient.Auth;
using HubconTestClient.Contracts;
using HubconTestClient.Models;
using HubconTestClient.Modules;
using HubconTestDomain;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

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

        int minCore = 0;
        int? maxCore = 0;

        int cores = maxCore ?? Environment.ProcessorCount - 1;

        for (int i = minCore; i <= cores; i++)
        {
            coreMask |= 1L << i;
        }

        process.ProcessorAffinity = (IntPtr)coreMask;
        process.PriorityClass = ProcessPriorityClass.RealTime;

        var builder = WebApplication.CreateSlimBuilder();

        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        builder.Services.AddHubconClient()
                        .AddRemoteServerModule<TestModule>()
                        .AddRemoteServerModule(() => new OpenAIServerModule(config));

        builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        var app = builder.Build();
        var scope = app.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IUserContract>();
        var authManager = scope.ServiceProvider.GetRequiredService<AuthenticationManager>();
        var client2 = scope.ServiceProvider.GetRequiredService<ISecondTestContract>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IUserContract>>();
        var openAi = scope.ServiceProvider.GetRequiredService<IOpenAIContract>();

        logger.LogInformation("Esperando interacción antes de iniciar las pruebas...");
        Console.ReadKey();

        //await TestOpenAiIntegration(logger, openAi);
        //await Task.Delay(100);

        await TestLogin(authManager, logger);
        await Task.Delay(100);

        //var responseTemp = await client.Execute(x => x.GetTemperatureFromServer("test"));

        //if (!responseTemp.Success || responseTemp.StatusCode != 200)
        //{
        //    // Hacer algo
        //}
        //else
        //{
        //    var data = responseTemp.Data;
        //    // Hago algo con data
        //}
        await TestHubconResponse(client2, logger);
        await Task.Delay(100);
        await TestValidations(client, logger);
        await Task.Delay(100);
        await TestIngest(client, logger);
        await Task.Delay(100);
        await TestInvokeNoParameters(client2, logger);
        await Task.Delay(100);
        await TestInvokeWithParameters(client, logger);
        await Task.Delay(100);
        await TestRemoteCancellation(client, logger);
        await Task.Delay(100);
        await TestSseStreaming(client, logger);
        await Task.Delay(100);


        int clientCount = 0;
        //_sw = Stopwatch.StartNew();
        //var ts = TimeSpan.FromSeconds(1);
        //var worker = new System.Timers.Timer();
        //worker.Interval = 1000;
        //worker.Elapsed += (sender, eventArgs) =>
        //{
        //    var avgRequestsPerSec = _finishedRequestsCount - _lastRequests;

        //    double avgLatency = 0;
        //    double p50 = 0, p95 = 0, p99 = 0;

        //    var latenciesSnapshot = Latencies.ToArray();
        //    Latencies.Clear();

        //    if (latenciesSnapshot.Length > 0)
        //    {
        //        Array.Sort(latenciesSnapshot);
        //        avgLatency = latenciesSnapshot.Average();

        //        p50 = Percentile(latenciesSnapshot, 50);
        //        p95 = Percentile(latenciesSnapshot, 95);
        //        p99 = Percentile(latenciesSnapshot, 99);
        //    }

        //    _maxReqs = Math.Max(_maxReqs, avgRequestsPerSec);

        //    logger.LogInformation($" Client count: {clientCount} | Requests: {_finishedRequestsCount} | Avg requests/s: {avgRequestsPerSec} | Max req/s: {_maxReqs} | " +
        //                          $"p50 latency(ms): {p50:F2} | p95 latency(ms): {p95:F2} | p99 latency(ms): {p99:F2} | Avg latency(ms): {avgLatency:F2}");

        //    var allocated = GC.GetTotalMemory(forceFullCollection: false);
        //    logger.LogInformation($"Heap Size: {allocated / 1024.0 / 1024.0:N2} MB - Time: {_sw.Elapsed}");

        //    _lastRequests = _finishedRequestsCount;
        //    _sw.Restart();
        //};
        //worker.Start();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 256
        };

        int rps = 9999999;

        //await Parallel.ForEachAsync(Enumerable.Range(0, int.MaxValue), options, async (i, ct) =>
        //{
        //TokenBucketRateLimiter tokenBucketRateLimiter = new TokenBucketRateLimiter(
        //    {
        //        QueueLimit = 1,
        //        AutoReplenishment = true,
        //        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        //        TokenLimit = rps,
        //        TokensPerPeriod = rps,
        //    });

        //    try
        //    {
        //        var paralellClient = scope.ServiceProvider.GetRequiredService<IUserContract>();
        //        Interlocked.Increment(ref clientCount);
        //        //await foreach(var item in client.GetMessages2())
        //        while (true)
        //        {
        //            // var swReq = Stopwatch.StartNew();
        //            //await tokenBucketRateLimiter.AcquireAsync();
        //            //await client.IngestMessages(GetMessages2(), default);
        //            //var item = await paralellClient.GetTemperatureFromServerWithInput(new TestInputClass(), ct);
        //            //await paralellClient.Execute(x => x.ShowTextOnServer());

        //            var item = await paralellClient.Execute(x => x.GetTemperatureFromServerWithInput(new TestInputClass(), ct));
        //            //Interlocked.Increment(ref _finishedRequestsCount);
        //        }
        //    }
        //    finally
        //    {
        //        Interlocked.Decrement(ref clientCount);
        //    }
        //});

        //Console.ReadKey();

        //var paralellClient = scope.ServiceProvider.GetRequiredService<IUserContract>();

        //while (true)
        //{
        //    if (paralellClient.TryGetAuthenticationManager(out var authenticationManager))
        //    {
        //        logger.LogInformation("Loggeando...");
        //        var result = await authenticationManager.LoginAsync("usuario", "");
        //        logger.LogInformation("Login: {IsSuccess}", result.IsSuccess);
        //        var request = await paralellClient.Execute(x => x.GetTemperatureFromServerWithInput(new TestInputClass()));
        //        logger.LogInformation("Request: {Data}", request.Data);
        //        await Task.Delay(1000);
        //        await authenticationManager.LogoutAsync();
        //        logger.LogInformation("Logged out.");
        //    }

        //    await Task.Delay(2000);
        //}

        //Console.ReadKey();

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

        int maxDegreeOfParallelism = options.MaxDegreeOfParallelism;
        var tasks = new List<Task>();

        for (int i = 0; i < maxDegreeOfParallelism; i++)
        {
            // Lanzamos cada worker como una Task independiente
            tasks.Add(Task.Run(async () =>
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
                    // Importante: Resolvemos el contrato dentro de la Task si el scope lo permite
                    var paralellClient = scope.ServiceProvider.GetRequiredService<IUserContract>();
                    Interlocked.Increment(ref clientCount);

                    while (true)
                    {
                        // El AcquireAsync es vital para no saturar si el Rate Limiter está activo
                        // await tokenBucketRateLimiter.AcquireAsync(1, ct); 

                        var item = await paralellClient.Execute(x => x.GetTemperatureFromServerWithInput(new TestInputClass(), default));

                        // Interlocked.Increment(ref _finishedRequestsCount);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Salida limpia por cancelación
                }
                finally
                {
                    Interlocked.Decrement(ref clientCount);
                }
            }, default));
        }

        // Esperamos a que todas las tareas terminen (esto ocurrirá cuando se cancele el ct)
        await Task.WhenAll(tasks);
    }

    private static async Task TestHubconResponse(ISecondTestContract client2, ILogger<IUserContract> logger)
    {
        var response = await client2.Execute(x => x.TestHubconResponse());
        if (response.Success && response.Data == true)
            logger.LogInformation($"Hubcon response OK.");
        else
            throw new Exception($"Error de test hubcon response: {response.Error}");
    }

    private static async Task TestOpenAiIntegration(ILogger<IUserContract> logger, IOpenAIContract openAi)
    {
        logger.LogInformation("Probando creación de modelo de respuesta...");
        var command = new CreateResponseCommand()
        {
            Model = "gpt-5-nano",
            Input = "Tell me a three sentence bedtime story about a unicorn."
        };

        var response = await openAi.Execute(x => x.CreateModelResponse(command));
        logger.LogInformation($"Respuesta: {response.Success}");
        await Task.Delay(1000);

        logger.LogInformation($"Probando stream SSE non-hubcon...");
        await Task.Delay(500);

        var request = new OpenAIStreamRequest()
        {
            Model = "gpt-5-nano",
            Input = "Dame una frase de 5 palabras sobre una manzana.",
        };

        var finalText = "";
        var streamResponse = await openAi.Execute(x => x.GetResponseStream(request));
        logger.LogInformation($"Respuesta: {streamResponse.Success}");
        await foreach (var item in streamResponse.Data!)
        {
            logger.LogInformation($"Event received: {item.Event}, data: {item.Delta}");
            finalText += item.Delta;
        }

        logger.LogInformation($"Final text: {finalText}");
        logger.LogInformation($"Stream SSE de tokens: OK");
        await Task.Delay(1000);

        logger.LogInformation($"Probando obtener inputs del modelo...");
        var response2 = await openAi.Execute(x => x.GetModelResponseInputs(response.Data.Id));
        logger.LogInformation($"Respuesta: {response2.Success}");
        await Task.Delay(1000);

        logger.LogInformation($"Probando obtener respuesta del modelo...");
        var response3 = await openAi.Execute(x => x.GetModelResponse(response.Data.Id));
        logger.LogInformation($"Respuesta: {response3.Success}");
        await Task.Delay(1000);

        logger.LogInformation($"Probando eliminar respuesta del modelo...");
        var response4 = await openAi.Execute(x => x.DeleteModelResponse(response3.Data.Id));
        logger.LogInformation($"Respuesta: {response4.Success}");
        await Task.Delay(1000);
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
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        bool temp2 = false;

        var result = await client.Execute(x => x.GetTemperatureFromServerBlocking(cts.Token));
        if (result.Failure)
        {
            logger.LogInformation($"Cancelación remota exitosa. Resultado: {result.Error}");
            temp2 = true;
        }
        else
        {
            throw new Exception($"La operación no fue cancelada como se esperaba. Resultado: {result.Data}");
        }
    }

    private static async Task TestInvokeWithParameters(IUserContract client, ILogger<IUserContract> logger)
    {
        logger.LogWarning("Probando invocación con retorno...");

        var response = await client.Execute(x => x.GetTemperatureFromServer(""));

        if (response.Success)
            logger.LogInformation($"Invocación OK. Datos recibidos: {response.Data}");
        else
            throw new Exception($"Error de invocacion.");
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

        async Task handler5(IEnumerable<int>? input)
        {
            logger.LogInformation($"Evento recibido: [{string.Join(",", input!)}]");
            Interlocked.Add(ref eventosRecibidos, 1);
            evento4 = true;
        }

        //await client.OnUserCreated.AddHandler(handler);
        //await client.OnUserCreated.Subscribe();
        //await client.OnUserCreated2.AddHandler(handler2);
        //await client.OnUserCreated2.Subscribe();
        //await client.OnUserCreated3.AddHandler(handler3);
        //await client.OnUserCreated3.Subscribe();
        //await client.OnUserCreated4.AddHandler(handler4);
        //await client.OnUserCreated4.Subscribe();
        //await client.OnEnumerableTest.AddHandler(handler5);
        //await client.OnEnumerableTest.Subscribe();

        logger.LogInformation("Eventos conectados.");

        await Task.Delay(100);

        logger.LogWarning("Enviando request de prueba...");
        var result = await client.Execute(x => x.CreateUser());
        logger.LogInformation($"Esperando eventos...");

        await Task.Delay(1000000);

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
        var response = await client2.Execute(x => x.TestReturn());

        if (response.Success)
            logger.LogInformation($"Invocación sin parametros OK.");
        else
            throw new Exception($"Error de Invocación sin parametros: {response.Error}");
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

        if (result.Success)
            logger.LogInformation($"Ingest OK.");
        else
            throw new Exception($"Ingest FAILED: {result.Message} | Error: {result.Error} | Exception: {result.Exception?.ToString()}");
    }

    private static async Task TestValidations(IUserContract client, ILogger<IUserContract> logger)
    {
        var response = await client.Execute(x => x.IngestMessages(GetMessages(10), null));

        if (response.Failure)
            logger.LogInformation($"Validaciones OK.");
        else
            throw new Exception($"Error de validaciones: {response.Error}");
    }

    private static async Task TestLogin(AuthenticationManager authManager, ILogger<IUserContract> logger)
    {
        logger.LogWarning($"Probando login...");
        var response = await authManager.LoginAsync("miusuario", "");

        if (response.IsSuccess)
            logger.LogInformation($"Login OK. Token: {authManager.TokenType}");
        else
            throw new Exception($"Error de login: {response.ErrorMessage}");
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