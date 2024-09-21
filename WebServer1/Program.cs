//WEBSERVER1/PROGRAM.CS
using System.Diagnostics;
using System.Net;
using System.Timers;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class Program
{
    private static readonly Dictionary<string, byte[]> FileCache = new Dictionary<string, byte[]>();
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
        FileCache.Remove(e.FullPath, out _);
    }

    private static void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        FileCache.Remove(e.OldFullPath, out _);
        FileCache.Remove(e.FullPath, out _);
    }

    //MAIN ------------------------------------------------------------------------------------------------------
    private static void Main(string[] args)
    {
        SetupEventlog();
        StartLoggingEngine();
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Realm = "localhost";
        listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        listener.Start();
        Console.WriteLine("Serving files from D:\\wwwroot\\ at http://localhost:8080/ SINGLE THREAD");

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            ProcessRequest(context);
        }
    }
    //END MAIN --------------------------------------------------------------------------------------------------

    private static void ProcessRequest(HttpListenerContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;
        string urlPath = request.Url.AbsolutePath;
        if (urlPath.EndsWith("/"))
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
                response.ContentType = GetContentType(localPath);
                response.ContentLength64 = content.Length;
                response.OutputStream.Write(content, 0, content.Length);
            }
            else
            {
                SendErrorResponse(response, HttpStatusCode.NotFound, "404 Not Found");
            }
        }
        catch (Exception ex)
        {
            Log(context.Request.RemoteEndPoint.Address.ToString(), "Anonymous", context.Request.HttpMethod, urlPath, context.Request.Url.Query, (int)HttpStatusCode.InternalServerError, -1, ex.HResult, stopwatch.Elapsed.TotalMilliseconds);
            ReportError($"Error processing request: {ex.Message}");
            try
            {
                SendErrorResponse(response, HttpStatusCode.InternalServerError, "500 Internal Server Error");
            }
            catch (Exception ex2)
            {
                ReportError($"Error sending error response: {ex2.Message}");
            }
        }
        finally
        {
            response.OutputStream.Close();
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
            ReportError($"Error sending error response: {ex.Message}");
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

    private string GetDebuggerDisplay()
    {
        return ToString() ?? string.Empty;
    }

    // Logging Engine

    private static List<string> logBuffer = new List<string>();
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
        flushTimer.Stop();
        flushTimer.Dispose();
        File.AppendAllLines(logFilePath, logBuffer);
    }

    private static void Log(string clientIP, string userName, string requestMethod, string urlStem, string urlQuery, int status, int subStatus, int win32Status, double timeTaken)
    {
        string logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {clientIP} {userName} {requestMethod} {urlStem} {urlQuery} {status} {subStatus} {win32Status} {timeTaken}ms";
        lock (logBuffer)
            logBuffer.Add(logEntry);
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
            EventLog eventLog = new() { Source = "WebServer", Log = "Application" };
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
