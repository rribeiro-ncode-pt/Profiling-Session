//WEBSERVER5/PROGRAM.CS
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Timers;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class Program
{
    private static int MaxThreads = 48;
    private static readonly int MaxQueueSize = 1000;
    private static readonly BlockingCollection<HttpListenerContext> RequestQueue =
        new BlockingCollection<HttpListenerContext>(new ConcurrentQueue<HttpListenerContext>(), MaxQueueSize);

    private static readonly ConcurrentDictionary<string, byte[]> FileCache = new ConcurrentDictionary<string, byte[]>();
    private static readonly FileSystemWatcher FileWatcher = new FileSystemWatcher(@"D:\wwwroot");

    static Program()
    {
        FileWatcher.IncludeSubdirectories = true;
        FileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        FileWatcher.Changed += OnFileChanged;
        FileWatcher.Created += OnFileChanged;
        FileWatcher.Deleted += OnFileChanged;
        FileWatcher.Renamed += OnFileRenamed;
        FileWatcher.EnableRaisingEvents = true;
    }

    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        FileCache.TryRemove(e.FullPath, out _);
    }

    private static void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        FileCache.TryRemove(e.OldFullPath, out _);
        FileCache.TryRemove(e.FullPath, out _);
    }

    //MAIN ------------------------------------------------------------------------------------------------------
    private static void Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int maxThreads))
        {
            MaxThreads = maxThreads;
        }
        else
        {
            var cores = Environment.ProcessorCount;
            MaxThreads = cores * 2;
        }
        SetupEventlog();
        StartLoggingEngine();
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Realm = "localhost";
        listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        listener.Start();
        Console.WriteLine($"Serving files from D:\\wwwroot\\ at http://localhost:8080/ with {MaxThreads} WORK THREAD - SYNC IO");

        for (int i = 0; i < MaxThreads; i++)
        {
            Thread workerThread = new Thread(ProcessRequests);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        while (true)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();

                RequestQueue.Add(context);
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Listener stopped: {ex.Message}");
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting request: {ex.Message}");
            }
        }

        listener.Stop();
        RequestQueue.CompleteAdding();
    }
    //END MAIN --------------------------------------------------------------------------------------------------

    private static void ProcessRequests()
    {
        foreach (var context in RequestQueue.GetConsumingEnumerable())
        {
            ProcessRequest(context);
        }
    }

    private static void ProcessRequest(HttpListenerContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;
        string urlPath = request.Url.AbsolutePath;
        if (urlPath.Length > 0 && urlPath[^1] == '/')
        {
            urlPath += "index.html";
        }

        string rootPath = @"D:\wwwroot";
        string localPath = Path.Combine(rootPath, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        localPath = Path.GetFullPath(localPath);
        rootPath = Path.GetFullPath(rootPath);
        try
        {

            if (!localPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                SendErrorResponse(response, HttpStatusCode.Forbidden, "403 Forbidden");
                return;
            }

            if (FileCache.TryGetValue(localPath, out byte[] content) || File.Exists(localPath) && (content = File.ReadAllBytes(localPath)) != null)
            {
                FileCache[localPath] = content;
                string contentType = GetContentType(localPath);
                response.ContentType = contentType;
                response.ContentLength64 = content.Length;
                if (contentType.StartsWith("image/"))
                {
                    SetImageCacheHeaders(response);
                }
                response.OutputStream.Write(content, 0, content.Length);
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.NotFound, "404 Not Found");
            }
            response.Close();
        }
        catch (Exception ex)
        {
            Log(context.Request.RemoteEndPoint.Address.ToString(), "Anonymous", context.Request.HttpMethod, urlPath, context.Request.Url.Query, (int)HttpStatusCode.InternalServerError, -1, ex.HResult, stopwatch.Elapsed.TotalMilliseconds);
            ReportError($"Error processing request: {ex.Message}");
            try
            {
                SendErrorResponse(response, HttpStatusCode.InternalServerError, "500 Internal Server Error");
                response.Close();
            }
            catch (Exception ex2)
            {
                response.Abort();
                ReportError($"Error sending error response: {ex2.Message}");
            }
        }
        finally
        {
            stopwatch.Stop();
            Log(context.Request.RemoteEndPoint.Address.ToString(), "Anonymous", context.Request.HttpMethod, urlPath, context.Request.Url.Query, response.StatusCode, 0, 0, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static void SendErrorResponse(HttpListenerResponse response, HttpStatusCode statusCode, string message)
    {
        try
        {
            response.StatusCode = (int)statusCode;
            byte[] content = System.Text.Encoding.UTF8.GetBytes(message);
            response.ContentType = "text/plain";
            response.ContentLength64 = content.Length;
            response.OutputStream.Write(content, 0, content.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending error response: {ex.Message}");
        }
    }

    private static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (extension)
        {
            case ".htm":
            case ".html":
                return "text/html";
            case ".css":
                return "text/css";
            case ".js":
                return "application/javascript";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".png":
                return "image/png";
            case ".gif":
                return "image/gif";
            case ".svg":
                return "image/svg+xml";
            case ".ico":
                return "image/x-icon";
            default:
                return "application/octet-stream";
        }
    }

    private static void SetImageCacheHeaders(HttpListenerResponse response)
    {
        response.Headers.Add("Cache-Control", "public, max-age=86400"); // 24 hours in seconds
        response.Headers.Add("Expires", DateTime.UtcNow.AddHours(24).ToString("R"));
    }

    private string GetDebuggerDisplay()
    {
        return ToString() ?? string.Empty;
    }


    // Logging Engine

    private static ConcurrentBag<string> logBuffer = new ConcurrentBag<string>();
    private static string logFilePath = $".\\logs\\server_log_{DateTime.Now.Ticks}.log";
    private static System.Timers.Timer flushTimer = new System.Timers.Timer(5000);

    private static void StartLoggingEngine()
    {
        if (!Path.Exists(".\\logs"))
            Directory.CreateDirectory(".\\logs");
        flushTimer.Elapsed += FlushLogBuffer;
        flushTimer.AutoReset = true;
        flushTimer.Enabled = true;
    }

    private static void StopLoggingEngine()
    {
        lock (logBuffer)
        {
            flushTimer.Stop();
            flushTimer.Dispose();
            File.AppendAllLines(logFilePath, logBuffer);
        }
    }

    private static void Log(string clientIP, string userName, string requestMethod, string urlStem, string urlQuery, int status, int subStatus, int win32Status, double timeTaken)
    {
        string logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {clientIP} {userName} {requestMethod} {urlStem} {urlQuery} {status} {subStatus} {win32Status} {timeTaken}ms";
        _ = Task.Run(() => logBuffer.Add(logEntry)); //log entry is added to the buffer in a separate thread
    }

    private static long MaxLogFileSize = 200 * 1024 * 1024; // 200MB

    private static void FlushLogBuffer(object? source, ElapsedEventArgs e)
    {
        try
        {
            lock (logBuffer)
            {
                if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length >= MaxLogFileSize)
                {
                    logFilePath = $".\\logs\\server_log_{DateTime.Now.Ticks}.log";
                }

                File.AppendAllLines(logFilePath, logBuffer);
                logBuffer.Clear(); // Clear the buffer after flushing
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error flushing log buffer: {ex.Message}");
        }
    }


    private static void ReportError(string errorMessage)
    {
        if (OperatingSystem.IsWindows())
        {
            EventLog eventLog = new EventLog("Application");
            eventLog.Source = "WebServer";
            eventLog.WriteEntry(errorMessage, EventLogEntryType.Error);
        }
        Console.WriteLine(errorMessage);
    }

    private static void SetupEventlog()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!EventLog.SourceExists("WebServer"))
            {
                EventLog.CreateEventSource("WebServer", "Application");
            }
        }
    }
}
