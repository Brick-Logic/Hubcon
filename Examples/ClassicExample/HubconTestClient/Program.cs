using Hubcon.Client;
using HubconTestClient.Auth;
using HubconTestClient.Modules;
using HubconTestDomain;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddHubconClient();
        builder.Services.AddRemoteServerModule<TestModule>(() => new TestModule(new object()));
        builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        var app = builder.Build();
        var scope = app.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IUserContract>();
        var authManager = scope.ServiceProvider.GetRequiredService<AuthenticationManager>();
        var client2 = scope.ServiceProvider.GetRequiredService<ISecondTestContract>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IUserContract>>();


        logger.LogInformation("Esperando interacción antes de iniciar las pruebas...");

        Console.ReadKey();

        logger.LogWarning($"Probando login...");
        var result = await authManager.LoginAsync("miusuario", "");
        logger.LogInformation("{0}", $"Login result: {result.IsSuccess}");
        logger.LogInformation($"Login OK.");

        await Task.Delay(100);

        logger.LogWarning($"Probando ingest...");
        var source1 = GetMessages(2);
        var source2 = GetMessages(2);
        var source3 = GetMessages(2);
        var source4 = GetMessages(2);
        var source5 = GetMessages(2);
        await client.IngestMessages2(source1, source2, source3, source4, source5);
        logger.LogInformation($"Ingest OK.");

        await Task.Delay(100);

        logger.LogWarning($"Probando invocación sin parametros...");
        var text = await client2.TestReturn();

        if (text != null)
            logger.LogInformation($"Invocación sin parametros OK.");
        else
            throw new Exception("Invocación sin parametros fallida.");

        await Task.Delay(100);

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

        client.OnUserCreated!.AddHandler(handler);
        await client.OnUserCreated.Subscribe();
        client.OnUserCreated2!.AddHandler(handler2);
        await client.OnUserCreated2.Subscribe();
        client.OnUserCreated3!.AddHandler(handler3);
        await client.OnUserCreated3.Subscribe();
        client.OnUserCreated4!.AddHandler(handler4);
        await client.OnUserCreated4.Subscribe();

        logger.LogInformation("Eventos conectados.");

        await Task.Delay(100);

        logger.LogWarning("Enviando request de prueba...");
        await client.CreateUser();
        logger.LogInformation($"Esperando eventos...");

        await Task.Delay(100);

        if (eventosRecibidos == 4)
        {
            logger.LogInformation($"Eventos recibidos correctamente.");
        }
        else
        {
            throw new Exception("No se recibieron todos los eventos esperados.");
        }

        await Task.Delay(100);

        logger.LogWarning("Probando invocación con retorno...");
        var temp = await client.GetTemperatureFromServer();
        logger.LogInformation($"Invocación OK. Datos recibidos: {temp}");

        await Task.Delay(100);

        logger.LogWarning("Probando cancelacion remota...");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        bool temp2 = false;
        try
        { 
            temp2 = await client.GetTemperatureFromServerBlocking(cts.Token);
        }
        catch (Exception e)
        {
            logger.LogInformation(e.ToString());
        }
        
        logger.LogInformation($"Cancelacion OK. Datos recibidos: {temp2}");
        
        await Task.Delay(100);

        logger.LogWarning("Probando streaming de 10 mensajes..."); 

        await foreach (var item in client.GetMessages(10))
        {
            logger.LogInformation($"Respuesta recibida: {item}");
        }

        logger.LogInformation("Streaming OK.");

        await Task.Delay(100);

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
            MaxDegreeOfParallelism = 25
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
                //Interlocked.Increment(ref clientCount);
                //await foreach(var item in client.GetMessages2())
                while (true)
                {
                    // var swReq = Stopwatch.StartNew();
                    try
                    {
                        await tokenBucketRateLimiter.AcquireAsync();
                        //await client.IngestMessages(GetMessages2(), default);
                        var item = await paralellClient.GetTemperatureFromServer(ct);
                        Interlocked.Increment(ref _finishedRequestsCount);
                    }
                    catch(Exception ex)
                    {
                        Interlocked.Increment(ref _errors);
                    }
                    finally
                    {
                        // swReq.Stop();
                        // Latencies.Add(swReq.Elapsed.TotalMilliseconds);
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref clientCount);
            }
        });
    }

    static async IAsyncEnumerable<string> GetMessages(int count)
    {
        for (int i = 0; i < count; i++)
        {
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

        while(true)
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