using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Reflection;
using System.IO.Compression;
using HandlerInterfaces;
using MiddlewareInterfaces;

namespace WebServer6
{

    class Program
    {
        private static ServerConfig config;
        private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private static readonly BlockingCollection<HttpListenerContext> RequestQueue = new BlockingCollection<HttpListenerContext>();

        static async Task Main(string[] args)
        {
            LoadConfiguration();
            StartLoggingEngine();

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{config.Port}/");
            listener.Prefixes.Add($"https://*:{config.HttpsPort}/");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;

            //TODO: Add timeout and Authentication Schema to the configuration file
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.TimeoutManager.IdleConnection = TimeSpan.FromMinutes(1);

            listener.Start();
            Console.WriteLine($"Server started on port {config.Port} (HTTP) and {config.HttpsPort} (HTTPS)");

            var processingTasks = new List<Task>();
            for (int i = 0; i < config.MaxThreads; i++)
            {
                processingTasks.Add(Task.Run(() => ProcessRequestsAsync(CancellationTokenSource.Token)));
            }

            while (!CancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    RequestQueue.Add(context);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await LogAsync($"Error accepting request: {ex.Message}");
                }
            }

            listener.Stop();
            RequestQueue.CompleteAdding();
            await Task.WhenAll(processingTasks);
        }

        private static async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            foreach (var context in RequestQueue.GetConsumingEnumerable(cancellationToken))
            {
                await HandleRequestAsync(context);
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var request = context.Request;
            var response = context.Response;

            try
            {
                var requestPath = request.Url.AbsolutePath;
                var requestMethod = request.HttpMethod;

                foreach (var middleware in config.Middleware)
                {
                    if (!await middleware.ProcessRequestAsync(context))
                    {
                        return;
                    }
                }

                IRequestHandler handler = null;
                foreach (var route in config.Routes)
                {
                    if (route.MatchesRequest(requestPath, requestMethod))
                    {
                        handler = route.Handler;
                        break;
                    }
                }

                if (handler == null)
                {
                    await SendErrorResponseAsync(response, HttpStatusCode.NotFound, "404 Not Found");
                    return;
                }

                await handler.HandleRequestAsync(request, response);
            }
            catch (Exception ex)
            {
                await LogAsync($"Error processing request: {ex.Message}\n{ex.StackTrace}");
                await SendErrorResponseAsync(response, HttpStatusCode.InternalServerError, "500 Internal Server Error");
            }
            finally
            {
                stopwatch.Stop();
                await LogAsync($"{context.Request.RemoteEndPoint} {request.HttpMethod} {request.RawUrl} {response.StatusCode} {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        private static void LoadConfiguration()
        {
            var json = File.ReadAllText("config.json");
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            config = JsonSerializer.Deserialize<ServerConfig>(json, options);

            config.InitializeHandlersAndModules();
        }

        private static async Task SendErrorResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string message)
        {
            response.StatusCode = (int)statusCode;
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private static Task LogAsync(string message)
        {
            //Console.WriteLine($"{DateTime.UtcNow}: {message}");
            return Task.CompletedTask;
        }

        private static void StartLoggingEngine()
        {

        }
    }

    class ServerConfig
    {
        public int Port { get; set; }
        public int HttpsPort { get; set; }
        public int MaxThreads { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public List<RouteConfig> RouteConfigs { get; set; }
        public List<MiddlewareConfig> MiddlewareConfigs { get; set; }

        public List<Route> Routes { get; private set; }
        public List<IMiddleware> Middleware { get; private set; }

        public void InitializeHandlersAndModules()
        {
            Routes = new List<Route>();
            foreach (var routeConfig in RouteConfigs)
            {
                try
                {
                    IRequestHandler handler;

                    var builtInType = typeof(Program).Assembly.GetType(routeConfig.ClassName);
                    if (builtInType != null)
                    {
                        handler = (IRequestHandler)Activator.CreateInstance(builtInType);
                    }
                    else
                    {
                        var assembly = Assembly.LoadFrom(routeConfig.AssemblyName);
                        var type = assembly.GetType(routeConfig.ClassName);

                        if (type == null)
                        {
                            throw new ArgumentException($"Class {routeConfig.ClassName} not found in assembly {routeConfig.AssemblyName}");
                        }

                        handler = (IRequestHandler)Activator.CreateInstance(type);
                    }

                    if (handler is IConfigurableHandler configurableHandler)
                    {
                        configurableHandler.Configure(routeConfig.Settings);
                    }

                    Routes.Add(new Route(routeConfig.Path, routeConfig.Methods, handler));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading handler {routeConfig.HandlerType}: {ex.Message}");
                }

                Routes = Routes.OrderBy(r => r).ToList();
            }

            Middleware = new List<IMiddleware>();
            foreach (var middlewareConfig in MiddlewareConfigs)
            {
                try
                {
                    IMiddleware middleware;

                    var builtInType = typeof(Program).Assembly.GetType(middlewareConfig.ClassName);
                    if (builtInType != null)
                    {
                        middleware = (IMiddleware)Activator.CreateInstance(builtInType);
                    }
                    else
                    {
                        var assembly = Assembly.LoadFrom(middlewareConfig.AssemblyName);
                        var type = assembly.GetType(middlewareConfig.ClassName);

                        if (type == null)
                        {
                            throw new ArgumentException($"Class {middlewareConfig.ClassName} not found in assembly {middlewareConfig.AssemblyName}");
                        }

                        middleware = (IMiddleware)Activator.CreateInstance(type);
                    }

                    if (middleware is IConfigurableMiddleware configurableMiddleware)
                    {
                        configurableMiddleware.Configure(middlewareConfig.Settings);
                    }

                    Middleware.Add(middleware);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading middleware {middlewareConfig.Name}: {ex.Message}");
                }
            }
        }
    }

    class RouteConfig
    {
        public string Path { get; set; }
        public List<string> Methods { get; set; }
        public string HandlerType { get; set; }
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }
        public Dictionary<string, JsonElement> Settings { get; set; }
    }

    class MiddlewareConfig
    {
        public string Name { get; set; }
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }
        public Dictionary<string, JsonElement> Settings { get; set; }
    }

    class Route : IComparable<Route>
    {
        public string Path { get; }
        public List<string> Methods { get; }
        public IRequestHandler Handler { get; }

        public Route(string path, List<string> methods, IRequestHandler handler)
        {
            Path = path;
            Methods = methods;
            Handler = handler;
        }

        public bool MatchesRequest(string requestPath, string requestMethod)
        {
            bool pathMatches = Path == "/"
                ? requestPath == "/"
                : Path.EndsWith("*")
                    ? requestPath.StartsWith(Path.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)
                    : requestPath.Equals(Path, StringComparison.OrdinalIgnoreCase);

            return pathMatches &&
                   (Methods.Contains("*") || Methods.Contains(requestMethod, StringComparer.OrdinalIgnoreCase));
        }

        public int CompareTo(Route other)
        {
            int pathComparison = other.Path.TrimEnd('*').Length.CompareTo(this.Path.TrimEnd('*').Length);
            if (pathComparison != 0) return pathComparison;

            if (this.Path.EndsWith("*") && !other.Path.EndsWith("*")) return 1;
            if (!this.Path.EndsWith("*") && other.Path.EndsWith("*")) return -1;

            return other.Methods.Count.CompareTo(this.Methods.Count);
        }
    }

    public class StaticFilesHandler : IConfigurableHandler
    {
        private string webRoot;
        private static readonly MemoryCache FileCache = new MemoryCache(new MemoryCacheOptions());
        private int cacheExpirationMinutes = 5;

        public void Configure(Dictionary<string, JsonElement> settings)
        {
            if (settings.TryGetValue("WebRoot", out var webRootElement))
            {
                webRoot = webRootElement.GetString();
            }
            else
            {
                throw new ArgumentException("WebRoot setting is required for StaticFilesHandler");
            }

            if (settings.TryGetValue("CacheExpirationMinutes", out var cacheExpirationElement))
            {
                cacheExpirationMinutes = cacheExpirationElement.GetInt32();
            }
            SetupFileWatcher();
        }

        private void SetupFileWatcher()
        {
            var fileWatcher = new FileSystemWatcher(webRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            fileWatcher.Changed += (s, e) => FileCache.Remove(e.FullPath);
            fileWatcher.Created += (s, e) => FileCache.Remove(e.FullPath);
            fileWatcher.Deleted += (s, e) => FileCache.Remove(e.FullPath);
            fileWatcher.Renamed += (s, e) =>
            {
                FileCache.Remove(e.OldFullPath);
                FileCache.Remove(e.FullPath);
            };
        }

        public async Task HandleRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var filePath = Path.Combine(webRoot, request.Url.AbsolutePath.TrimStart('/'));

            if (!File.Exists(filePath))
            {
                await SendErrorResponseAsync(response, HttpStatusCode.NotFound, "404 Not Found");
                return;
            }

            if (!FileCache.TryGetValue(filePath, out byte[] content))
            {
                content = File.ReadAllBytes(filePath);
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(cacheExpirationMinutes));
                FileCache.Set(filePath, content, cacheEntryOptions);
            }

            response.ContentType = GetMimeType(filePath);
            response.ContentLength64 = content.Length;
            SetSecurityHeaders(response);

            if (response.ContentType.StartsWith("image/"))
            {
                SetImageCacheHeaders(response);
            }

            await response.OutputStream.WriteAsync(content, 0, content.Length);
            response.Close();
        }

        private void SetSecurityHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("X-Content-Type-Options", "nosniff");
            response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
            //response.Headers.Add("Content-Security-Policy", "default-src 'self'");
        }

        private void SetImageCacheHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("Cache-Control", "public, max-age=86400");
            response.Headers.Add("Expires", DateTime.UtcNow.AddMinutes(5).ToString("R"));
        }

        private async Task SendErrorResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string message)
        {
            response.StatusCode = (int)statusCode;
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".html":
                case ".htm":
                    return "text/html";
                case ".css":
                    return "text/css";
                case ".js":
                    return "application/javascript";
                case ".json":
                    return "application/json";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".ico":
                    return "image/x-icon";
                case ".svg":
                    return "image/svg+xml";
                case ".pdf":
                    return "application/pdf";
                case ".zip":
                    return "application/zip";
                case ".txt":
                    return "text/plain";
                default:
                    return "application/octet-stream";
            }
        }
    }


    public class SampleDynamicContentHandler : IConfigurableHandler
    {
        private string defaultResponseType = "text/html";

        public void Configure(Dictionary<string, JsonElement> settings)
        {
            if (settings.TryGetValue("DefaultResponseType", out var responseTypeElement))
            {
                defaultResponseType = responseTypeElement.GetString();
            }
        }

        public async Task HandleRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string content;
            if (defaultResponseType == "application/json")
            {
                content = JsonSerializer.Serialize(new { message = $"Dynamic Content from {request.Url.AbsolutePath}", timestamp = DateTime.UtcNow });
            }
            else
            {
                content = $"<html><body><h1>Dynamic Content</h1><p>Hello from {request.Url.AbsolutePath}</p></body></html>";
            }

            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentType = defaultResponseType;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
    }

    public class APIAuthenticationModule : IConfigurableMiddleware
    {
        private string ApiKey { get; set; }
        private List<string> PathCollection { get; set; }

        public void Configure(Dictionary<string, JsonElement> settings)
        {
            if (settings.TryGetValue("ApiKey", out var apiKeyElement))
            {
                ApiKey = apiKeyElement.GetString();
            }
            else
            {
                throw new ArgumentException("ApiKey setting is required for AuthenticationModule");
            }
            if (settings.TryGetValue("Path", out var pathCollection))
            {
                PathCollection = pathCollection.EnumerateArray().Select(p => p.GetString()).ToList();
            }
        }

        public Task<bool> ProcessRequestAsync(HttpListenerContext context)
        {
            if (PathCollection != null && !PathCollection.Any((path) => context.Request.Url.AbsolutePath.StartsWith(path)))
            {
                return Task.FromResult(true);
            }
            else if (context.Request.Headers["X-API-Key"] != ApiKey)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Close();
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }

    public class CompressionModule : IConfigurableMiddleware
    {
        private int MinSizeToCompress { get; set; }

        public void Configure(Dictionary<string, JsonElement> settings)
        {
            if (settings.TryGetValue("MinSizeToCompress", out var minSizeElement))
            {
                MinSizeToCompress = minSizeElement.GetInt32();
            }
            else
            {
                MinSizeToCompress = 1024; // Default value
            }
        }

        public async Task<bool> ProcessRequestAsync(HttpListenerContext context)
        {
            var response = context.Response;
            var acceptEncoding = context.Request.Headers["Accept-Encoding"];

            if (acceptEncoding != null && response.ContentLength64 >= MinSizeToCompress)
            {
                if (acceptEncoding.Contains("gzip"))
                {
                    response.AddHeader("Content-Encoding", "gzip");
                    using (var gzipStream = new GZipStream(response.OutputStream, CompressionMode.Compress))
                    {
                        await context.Response.OutputStream.CopyToAsync(gzipStream);
                    }
                }
                else if (acceptEncoding.Contains("deflate"))
                {
                    response.AddHeader("Content-Encoding", "deflate");
                    using (var deflateStream = new DeflateStream(response.OutputStream, CompressionMode.Compress))
                    {
                        await context.Response.OutputStream.CopyToAsync(deflateStream);
                    }
                }
            }

            return true;
        }
    }

    public class RateLimitingModule : IConfigurableMiddleware
    {
        private readonly ConcurrentDictionary<string, (int Count, DateTime LastReset)> requestCounts = new ConcurrentDictionary<string, (int, DateTime)>();
        private int MaxRequestsPerMinute { get; set; }

        public void Configure(Dictionary<string, JsonElement> settings)
        {
            if (settings.TryGetValue("MaxRequestsPerMinute", out var maxRequestsElement))
            {
                MaxRequestsPerMinute = maxRequestsElement.GetInt32();
            }
            else
            {
                MaxRequestsPerMinute = 60; // Default value
            }
        }

        public Task<bool> ProcessRequestAsync(HttpListenerContext context)
        {
            var ipAddress = context.Request.RemoteEndPoint.Address.ToString();
            var now = DateTime.UtcNow;

            if (!requestCounts.TryGetValue(ipAddress, out var currentCount))
            {
                currentCount = (0, now);
            }

            if ((now - currentCount.LastReset).TotalMinutes >= 1)
            {
                currentCount = (1, now);
            }
            else if (currentCount.Count >= MaxRequestsPerMinute)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.Close();
                return Task.FromResult(false);
            }
            else
            {
                currentCount.Count++;
            }

            requestCounts[ipAddress] = currentCount;
            return Task.FromResult(true);
        }
    }
}