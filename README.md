# Project: Profiling Session

This project is a sample .NET 8 solution designed to train developers in CPU and memory profiling using Visual Studio and other tools.

## Introduction

Performance is a critical aspect of any application, especially as data volume and user numbers grow. Profiling is the process of analyzing an application to identify and diagnose performance issues such as excessive CPU usage, memory consumption, or I/O bottlenecks. This project aims to demonstrate how to use different profiling tools to improve the efficiency of .NET applications.

## Overview

The solution consists of several components:

- [ClientLoad](#clientload)
- [StockPageGenerator](#stockpagegenerator)
- [WebServers (1 to 5)](#webservers-1-to-5)

### Components

1. **ClientLoad**: Simulates multiple client requests to generate load on the web server. It uses `HttpClient` to send requests and measures response times, collecting statistics such as average response time, minimum and maximum response times, and the total number of requests processed.

2. **StockPageGenerator**: Generates/Updates a fictitious NASDAQ stock quotes page every second.

3. **WebServers (1 to 5)**: Five different implementations of a simple HTTP server with various strategies to handle client requests.

### WebServer Implementations

The original developer of this prototype doesn't believe in config files, all setting are hardcoded. All the versions serve anything inside `(D:\wwwroot\)` as long as it is static and the ContentType is one of these:
**.htm**, **.html**: `text/html`
**.css**: `text/css`
**.js**: `application/javascript`
**.jpg**, **.jpeg**: `image/jpeg`
**.png**: `image/png`
**.gif**: `image/gif`
**.svg**: `image/svg+xml`
**.ico**: `image/x-icon`
**default**: `application/octet-stream`


- **WebServer1**: Synchronous single-threaded server using `HttpListener`.
- **WebServer2**: Similar to WebServer1, but uses `ConcurrentDictionary` and `Task.Run` to parallelize processing.
- **WebServer3**: Asynchronous implementation with `async/await` for improved I/O performance.
- **WebServer4**: Utilizes `BlockingCollection` to manage a request queue and dedicated worker threads. 
- **WebServer5**: Extends WebServer4 with cache headers for images.

For 4 and 5 you can specify how many worker threads to use in the command line 
```powershell
   PS C:\Projects\ProfileSession\WebServer4\bin\Release\net8.0> WebServer4.exe 2 <-- (number of worker threads)
   PS C:\Projects\ProfileSession\WebServer5\bin\Release\net8.0> WebServer5.exe 8 <-- (number of worker threads)
```


## How to Run

1. Clone the repository:
   ```powershell
   git clone https://github.com/rribeiro-ncode-pt/Profiling-Session.git
   ```
2. Navigate to the project directory:
   ```powershell
   cd ProfilingSession (Your directory)
   ```
3. Build the project:
   ```powershell
   dotnet build
   ```
4. Run the desired WebServer:
   ```powershell
   dotnet run --project src/WebServer1
   ```
   Replace `WebServer1` with the desired version (e.g., WebServer2, WebServer3, etc.).

5. Open a browser and navigate to `http://localhost:8080` (as always, hardcoded) to access the hosted files.

## Recommended Profiling Tools

### CPU Profiling Tools

- **Visual Studio Profiler**: Analyze CPU usage.
- **PerfView**: A detailed performance analysis tool, particularly powerful for CPU profiling, garbage collection analysis, and thread contention. (Not the prettiest UI, but very effective.)
- **dotnet-trace**: Collects low-level performance traces to analyze CPU and other resource usage.
- **dotTrace (JetBrains)**: A powerful profiler for CPU analysis.

### Memory Profiling Tools

- **dotMemory (JetBrains)**: Focuses on memory usage analysis, including object allocations and retention paths.
- **dotnet-dump**: Captures process dumps to diagnose memory issues.

### Production Server Collection/Monitoring Tools

- **dotnet-trace**: Collects low-level performance traces to analyze CPU and other resource usage.
- **PerfView**: Same as above BUT used with extreme care!
- **dotnet-counters**: Monitor real-time performance metrics.
- **Windows Performance Analyzer (WPA)**: Tool for visualizing and analyzing Windows performance data.

### Web Application Analysis Tools

- **Edge/Chrome DevTools Profiler**: Client-side JavaScript and performance profiling directly within the browser's developer tools.

## Learning Objectives

- **When to Profile**: Identify the appropriate times to use profiling tools during development.
- **When to Optimize**: Understand when optimization is necessary and the trade-offs involved.
- **Understand the Mindset**: Develop a critical thinking approach to identifying performance issues.
- **Analyze and Interpret Performance Metrics**: A brief introduction to interpreting profiling metrics.
- **Use Profiling Tools to Identify and Resolve Performance Issues**: Practical examples of using tools like Visual Studio Profiler, PerfView, and dotMemory to identify common problems.

## Table of Contents

- [Introduction](#introduction)
- [Overview](#overview)
  - [Components](#components)
  - [WebServer Implementations](#webserver-implementations)
- [How to Run](#how-to-run)
- [Recommended Profiling Tools](#recommended-profiling-tools)
- [Learning Objectives](#learning-objectives)

