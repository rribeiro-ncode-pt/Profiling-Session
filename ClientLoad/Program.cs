using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace LoadTester
{
    class Program
    {
        private static readonly List<string> FilesToRequest = new List<string>
        {
            "/",              // Root (should serve index.html)
            "/favicon.ico",
            "/index.html",
            "/images/favicon.bmp",
            "/images/logo-big.png",
            "/images/manutencao_dark.png",
            "/images/signature_logo.png",
            "/public/favicon.ico",
            "/public/index.html",
            "/public/privacy-policy.html",
            "/public/src/components/footer.html",
            "/public/src/components/header.html",
            "/public/src/css/all.min.css",
            "/public/src/css/main.css",
            "/public/src/css/privacy.css",
            "/public/src/img/consulting.png",
            "/public/src/img/consulting_sepia.jpg",
            "/public/src/img/consulting_sepia.png",
            "/public/src/img/debugging.png",
            "/public/src/img/debugging_sepia.jpg",
            "/public/src/img/debugging_sepia.png",
            "/public/src/img/high-performance.png",
            "/public/src/img/high-performance_sepia.jpg",
            "/public/src/img/high-performance_sepia.png",
            "/public/src/img/integration.png",
            "/public/src/img/integration_sepia.jpg",
            "/public/src/img/integration_sepia.png",
            "/public/src/img/logo-big-inset.png",
            "/public/src/img/logo-big.png",
            "/public/src/img/logo-big.svg",
            "/public/src/img/logo.png",
            "/public/src/js/main.js",
            "/public/src/js/privacy.js",
            "/public/src/webfonts/fa-solid-900.ttf",
            "/public/src/webfonts/fa-solid-900.woff",
            "/public/src/webfonts/fa-solid-900.woff2",
            "/nonexistent.html"
        };

        private static int NumberOfClients = 300;

        private static int RequestsPerClient = 2000;

        private static double TotalElapsedTime = 0.0;

        private static readonly string ServerUrl = "http://localhost:8080";

        private static readonly ConcurrentBag<long> ResponseTimes = new ConcurrentBag<long>();

        private static readonly object LockObject = new object();

        static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out NumberOfClients))
                {
                    Console.WriteLine("Invalid value for NumberOfClients. Using default value.");
                }
                if (args.Length > 1)
                {
                    if (!int.TryParse(args[1], out RequestsPerClient))
                    {
                        Console.WriteLine("Invalid value for RequestsPerClient. Using default value.");
                    }
                }
            }

            Console.WriteLine($"Starting load test with {NumberOfClients} clients, each making {RequestsPerClient} requests.");

            List<Task> clientTasks = new List<Task>();
            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < NumberOfClients; i++)
            {
                clientTasks.Add(Task.Run(() => SimulateClient(i)));
            }

            await Task.WhenAll(clientTasks);
            stopwatch.Stop();
            TotalElapsedTime = stopwatch.ElapsedMilliseconds;
            DisplayStatistics();
        }

        private static async Task SimulateClient(int clientId)
        {
            List<long> rt = new List<long>(RequestsPerClient); 
            using (HttpClient client = new HttpClient())
            {
                for (int i = 0; i < RequestsPerClient; i++)
                {
                    string file = GetRandomFile();
                    string url = $"{ServerUrl}{file}";

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(url);
                        stopwatch.Stop();
                        rt.Add(stopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        Console.WriteLine($"Client {clientId}: Error requesting {file}: {ex.Message}");
                    }
                };
            }
            foreach (var time in rt)
            {
                ResponseTimes.Add(time);
            }
        }

        private static string GetRandomFile()
        {
            Random random = new Random();
            int index = random.Next(FilesToRequest.Count);
            return FilesToRequest[index];
        }

        private static void DisplayStatistics()
        {
            Console.WriteLine("\nLoad Test Completed.");
            Console.WriteLine($"Total Requests: {ResponseTimes.Count}");

            if (ResponseTimes.Count > 0)
            {
                double averageTime = ResponseTimes.Average();
                long maxTime = ResponseTimes.Max();
                long minTime = ResponseTimes.Min();

                Console.WriteLine($"Average Response Time: {averageTime:F2} ms");
                Console.WriteLine($"Minimum Response Time: {minTime} ms");
                Console.WriteLine($"Maximum Response Time: {maxTime} ms");
                Console.WriteLine($"Requests per second: {ResponseTimes.Count / TotalElapsedTime*1000:F2}");
                Console.WriteLine($"Total Elapsed Time: {TotalElapsedTime} ms");
                Console.WriteLine($"Total Time: {ResponseTimes.Sum() / 1000} s");
            }
            else
            {
                Console.WriteLine("No responses received.");
            }
        }
    }
}
