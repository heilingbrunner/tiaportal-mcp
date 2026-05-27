using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer
{
    public class Program
    {
        // Upper bound for how long the HTTP bridge waits on the MCP server's
        // response for one request. Bounds the DoS surface where a malformed
        // or unanswerable request would otherwise hold the gate forever.
        private const int ResponseTimeoutSeconds = 60;

        public static async Task Main(string[] args)
        {
            var options = CliOptions.ParseArgs(args);

            Engineering.TiaMajorVersion = options.TiaMajorVersion ?? 20;

            if (Engineering.TiaMajorVersion < 20)
            {
                AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;
            }
            else
            {
                Openness.Initialize(Engineering.TiaMajorVersion);
            }

            // Ensure user is in user group 'Siemens TIA Openness'
            if (await Openness.IsUserInGroup())
            {
                if (string.Equals(options.Transport, "http", StringComparison.OrdinalIgnoreCase))
                {
                    await RunHttpHost(options);
                }
                else
                {
                    await RunStdioHost(options);
                }
            }
            else
            {
                Console.WriteLine("User is not in the required group. Exiting...");
            }
        }

        public static async Task RunStdioHost(CliOptions? options)
        {
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            if (builder != null)
            {
                if (options != null && options.Logging != null)
                {
                    switch (options.Logging)
                    {
                        case 1:
                            // ATTENTION: For STDIO, logs must go to stderr!
                            builder.Logging.AddConsole(options =>
                            {
                                options.LogToStandardErrorThreshold = LogLevel.Trace;
                            });
                            break;

                        case 2:
                            // Visual Studio Debug Output / Sysinternals.DebugView
                            builder.Logging.AddDebug();
                            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Information);
                            builder.Logging.AddFilter("TiaMcpServer", LogLevel.Debug);

                            // Log Level for Debug Output
                            builder.Logging.SetMinimumLevel(LogLevel.Debug);
                            break;

                        case 3:
                            // Windows Event Log
                            builder.Logging.AddEventLog();
                            break;

                        default:
                            // no logging
                            break;
                    }
                }

                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly()
                    .WithPromptsFromAssembly();

                // Register the Portal service for dependency injection
                builder.Services.AddSingleton<Portal>();

                var host = builder.Build();

                // Set the service provider for the MCP server, to retrieve Portal with injected logger
                McpServer.SetServiceProvider(host.Services);

                // Set the logger for the MCP server
                McpServer.Logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer");

                // log a bit of information about the server start
                if (options != null && options.Logging != null && options.Logging > 0)
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();

                    logger.LogInformation($"=== TIA Portal MCP Server '{DateTime.Now.ToShortTimeString()}' ===");

                    switch (options.Logging)
                    {
                        case 1:
                            logger.LogInformation("Logging to stderr");
                            break;
                        case 2:
                            logger.LogInformation("Logging to debug output");
                            break;
                        case 3:
                            logger.LogInformation("Logging to Windows event log");
                            break;
                    }
                }

                await host.RunAsync();
            }

        }

        public static async Task RunHttpHost(CliOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Duplex pipe bridge: HTTP loop writes JSON-RPC requests to inboundPipe,
            // MCP server reads from inboundPipe.Reader; server writes responses to
            // outboundPipe.Writer, HTTP loop reads from outboundPipe.Reader.
            var inboundPipe = new Pipe();
            var outboundPipe = new Pipe();
            var serverInputStream = inboundPipe.Reader.AsStream();
            var serverOutputStream = outboundPipe.Writer.AsStream();

            var builder = Host.CreateEmptyApplicationBuilder(settings: null);

            if (options.Logging != null)
            {
                switch (options.Logging)
                {
                    case 1:
                        // HTTP transport: stdout is free, so a normal console sink is fine.
                        builder.Logging.AddConsole();
                        break;

                    case 2:
                        builder.Logging.AddDebug();
                        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Information);
                        builder.Logging.AddFilter("TiaMcpServer", LogLevel.Debug);
                        builder.Logging.SetMinimumLevel(LogLevel.Debug);
                        break;

                    case 3:
                        builder.Logging.AddEventLog();
                        break;

                    default:
                        break;
                }
            }

            builder.Services
                .AddMcpServer()
                .WithStreamServerTransport(serverInputStream, serverOutputStream)
                .WithToolsFromAssembly()
                .WithPromptsFromAssembly();

            builder.Services.AddSingleton<Portal>();

            var host = builder.Build();

            McpServer.SetServiceProvider(host.Services);
            McpServer.Logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer");

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation($"=== TIA Portal MCP Server '{DateTime.Now.ToShortTimeString()}' (http) ===");
            logger.LogInformation("HTTP transport listening on {Prefix}", options.HttpPrefix);

            using (var cts = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler cancelHandler = (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                Console.CancelKeyPress += cancelHandler;

                var listener = new HttpListener();
                listener.Prefixes.Add(options.HttpPrefix);

                try
                {
                    listener.Start();
                }
                catch (HttpListenerException ex)
                {
                    logger.LogError(ex, "Failed to start HttpListener on {Prefix}", options.HttpPrefix);
                    Console.CancelKeyPress -= cancelHandler;
                    return;
                }

                // Patch 3: per-id correlation via a demuxer that owns the outbound pipe.
                // The single SemaphoreSlim gate from Patch 1 is gone -- it serialized the
                // *whole* request/response cycle and produced head-of-line blocking when one
                // tool was slow. Now requests are written under a tiny writeLock (held only
                // for the frame write, not the await) and responses are dispatched back to
                // per-request TaskCompletionSources by JSON-RPC id.
                var waiters = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
                // Patch 4: SSE side pipe. Active GET /mcp/sse listeners get every server-to-client
                // notification (and any server-initiated request) the SDK emits on the outbound pipe.
                var sseSubscribers = new ConcurrentDictionary<Guid, SseSubscriber>();
                var writeLock = new SemaphoreSlim(1, 1);
                var hostTask = host.RunAsync(cts.Token);
                var demuxerTask = DemuxOutboundAsync(outboundPipe.Reader, waiters, sseSubscribers, logger, cts.Token);
                var listenerTask = AcceptLoopAsync(listener, inboundPipe.Writer, writeLock, waiters, sseSubscribers, options.HttpApiKey, logger, cts.Token);

                try
                {
                    await Task.WhenAny(hostTask, listenerTask, demuxerTask);
                }
                finally
                {
                    cts.Cancel();
                    try { listener.Stop(); } catch { }
                    listener.Close();

                    // Tear down any still-open SSE subscribers so their handler tasks unblock.
                    foreach (var key in sseSubscribers.Keys)
                    {
                        if (sseSubscribers.TryRemove(key, out var sub))
                        {
                            sub.Dispose();
                        }
                    }

                    // Closing the inbound writer signals EOF to the MCP server so it shuts down.
                    inboundPipe.Writer.Complete();
                    try { await hostTask; } catch (OperationCanceledException) { }
                    try { await listenerTask; } catch (OperationCanceledException) { }
                    try { await demuxerTask; } catch (OperationCanceledException) { }

                    Console.CancelKeyPress -= cancelHandler;
                }
            }
        }

        private static async Task AcceptLoopAsync(
            HttpListener listener,
            PipeWriter inboundWriter,
            SemaphoreSlim writeLock,
            ConcurrentDictionary<int, TaskCompletionSource<string>> waiters,
            ConcurrentDictionary<Guid, SseSubscriber> sseSubscribers,
            string? apiKey,
            ILogger logger,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => HandleRequestAsync(context, inboundWriter, writeLock, waiters, sseSubscribers, apiKey, logger, ct), ct);
            }
        }

        private static async Task HandleRequestAsync(
            HttpListenerContext context,
            PipeWriter inboundWriter,
            SemaphoreSlim writeLock,
            ConcurrentDictionary<int, TaskCompletionSource<string>> waiters,
            ConcurrentDictionary<Guid, SseSubscriber> sseSubscribers,
            string? apiKey,
            ILogger logger,
            CancellationToken ct)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // SSE side pipe: GET /mcp/sse subscribes to server-to-client notifications.
                // Branch before the POST validation so the SSE handler can own the response lifetime.
                if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                    && request.Url != null
                    && string.Equals(request.Url.AbsolutePath, "/mcp/sse", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSseAsync(context, sseSubscribers, apiKey, logger, ct).ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(response, 405, "Method Not Allowed").ConfigureAwait(false);
                    return;
                }

                if (request.Url == null || !string.Equals(request.Url.AbsolutePath, "/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(response, 404, "Not Found").ConfigureAwait(false);
                    return;
                }

                var contentType = request.ContentType ?? string.Empty;
                if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteStatusAsync(response, 400, "Content-Type must be application/json").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    var presented = request.Headers["X-API-Key"];
                    if (string.IsNullOrEmpty(presented) || !string.Equals(presented, apiKey, StringComparison.Ordinal))
                    {
                        await WriteStatusAsync(response, 401, "Unauthorized").ConfigureAwait(false);
                        return;
                    }
                }

                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    await WriteStatusAsync(response, 400, "Empty body").ConfigureAwait(false);
                    return;
                }

                var idKind = TryExtractRequestId(body, out int requestId);
                switch (idKind)
                {
                    case IdExtraction.InvalidJson:
                        await WriteStatusAsync(response, 400, "Invalid JSON").ConfigureAwait(false);
                        return;
                    case IdExtraction.UnsupportedId:
                        await WriteStatusAsync(response, 400, "JSON-RPC id must be a 32-bit integer (string/float ids not supported by this bridge)").ConfigureAwait(false);
                        return;
                }

                var payload = Encoding.UTF8.GetBytes(body.Trim() + "\n");

                if (idKind == IdExtraction.Notification)
                {
                    // Notification: write under writeLock, no response expected.
                    await writeLock.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        await inboundWriter.WriteAsync(payload, ct).ConfigureAwait(false);
                        await inboundWriter.FlushAsync(ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        writeLock.Release();
                    }
                    await WriteStatusAsync(response, 202, "Accepted").ConfigureAwait(false);
                    return;
                }

                // Request path: register a per-id TCS so the demuxer can route the response back.
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!waiters.TryAdd(requestId, tcs))
                {
                    await WriteStatusAsync(response, 400, $"Duplicate in-flight JSON-RPC id {requestId}; client must use unique ids per outstanding request").ConfigureAwait(false);
                    return;
                }

                try
                {
                    // Per-request budget (Patch 1). Outer ct still drives shutdown.
                    using (var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        requestCts.CancelAfter(TimeSpan.FromSeconds(ResponseTimeoutSeconds));

                        // Inbound write -- briefly serialized so JSON-RPC frames don't interleave on the wire.
                        // The lock is NOT held across the await on the response. That's the whole point of Patch 3.
                        await writeLock.WaitAsync(requestCts.Token).ConfigureAwait(false);
                        try
                        {
                            await inboundWriter.WriteAsync(payload, requestCts.Token).ConfigureAwait(false);
                            await inboundWriter.FlushAsync(requestCts.Token).ConfigureAwait(false);
                        }
                        finally
                        {
                            writeLock.Release();
                        }

                        // Bridge the timeout into the TCS via a cancellation callback. When the linked
                        // CTS fires, TrySetCanceled() unblocks the await with OperationCanceledException.
                        // Late demuxer dispatches after we've cancelled will TrySetResult on a cancelled
                        // TCS (no-op) and we will have already removed our dict entry in the outer finally.
                        using (requestCts.Token.Register(() => tcs.TrySetCanceled()))
                        {
                            try
                            {
                                var responseLine = await tcs.Task.ConfigureAwait(false);
                                var bytes = Encoding.UTF8.GetBytes(responseLine);
                                response.StatusCode = 200;
                                response.ContentType = "application/json";
                                response.ContentLength64 = bytes.LongLength;
                                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                            {
                                logger.LogWarning("Request id={Id} exceeded {Seconds}s response budget; returning 504", requestId, ResponseTimeoutSeconds);
                                await WriteStatusAsync(response, 504, $"Gateway Timeout: MCP server did not respond within {ResponseTimeoutSeconds}s").ConfigureAwait(false);
                            }
                        }
                    }
                }
                finally
                {
                    waiters.TryRemove(requestId, out _);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown in progress; swallow.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while serving /mcp request");
                try
                {
                    await WriteStatusAsync(response, 500, "Internal Server Error").ConfigureAwait(false);
                }
                catch
                {
                    // Response may already be partially flushed; nothing more to do.
                }
            }
            finally
            {
                try { response.OutputStream.Close(); } catch { }
                try { response.Close(); } catch { }
            }
        }

        private enum IdExtraction
        {
            Notification,
            IntId,
            UnsupportedId,
            InvalidJson,
        }

        private static IdExtraction TryExtractRequestId(string body, out int id)
        {
            id = 0;
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return IdExtraction.InvalidJson;
                    }
                    if (!doc.RootElement.TryGetProperty("id", out var idElement))
                    {
                        return IdExtraction.Notification;
                    }
                    if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out id))
                    {
                        return IdExtraction.IntId;
                    }
                    return IdExtraction.UnsupportedId;
                }
            }
            catch (JsonException)
            {
                return IdExtraction.InvalidJson;
            }
        }

        private static async Task DemuxOutboundAsync(
            PipeReader outboundReader,
            ConcurrentDictionary<int, TaskCompletionSource<string>> waiters,
            ConcurrentDictionary<Guid, SseSubscriber> sseSubscribers,
            ILogger logger,
            CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await ReadLineAsync(outboundReader, ct).ConfigureAwait(false);
                    if (line == null)
                    {
                        // Outbound pipe closed -- SDK shut down. Exit cleanly; the finally below
                        // cancels any still-pending waiters so their handlers don't hang.
                        return;
                    }
                    await DispatchOutboundFrameAsync(line, waiters, sseSubscribers, logger, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown.
            }
            finally
            {
                // Anyone still awaiting now will never get a response from the SDK -- cancel them
                // so their HandleRequestAsync surfaces 504/cleanup instead of hanging on a dead pipe.
                foreach (var kv in waiters)
                {
                    kv.Value.TrySetCanceled();
                }
            }
        }

        private static async Task DispatchOutboundFrameAsync(
            string line,
            ConcurrentDictionary<int, TaskCompletionSource<string>> waiters,
            ConcurrentDictionary<Guid, SseSubscriber> sseSubscribers,
            ILogger logger,
            CancellationToken ct)
        {
            try
            {
                bool hasId;
                bool hasMethod;
                bool idIsInt = false;
                int id = 0;
                using (var doc = JsonDocument.Parse(line))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        logger.LogDebug("Demuxer: non-object outbound frame discarded ({Len} bytes)", line.Length);
                        return;
                    }
                    hasId = doc.RootElement.TryGetProperty("id", out var idElement);
                    hasMethod = doc.RootElement.TryGetProperty("method", out _);
                    if (hasId && idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out var parsedId))
                    {
                        idIsInt = true;
                        id = parsedId;
                    }
                }

                // No id => notification (progress, logging, listChanged, ...). Broadcast on SSE.
                if (!hasId)
                {
                    await BroadcastSseAsync(line, sseSubscribers, logger, ct).ConfigureAwait(false);
                    return;
                }

                // id + method => server-initiated request (sampling/createMessage, elicitation, ...).
                // The client receiving the SSE stream is expected to handle these; we relay verbatim.
                if (hasMethod)
                {
                    await BroadcastSseAsync(line, sseSubscribers, logger, ct).ConfigureAwait(false);
                    return;
                }

                // id, no method => response to a previous POST. Route to the waiter.
                if (!idIsInt)
                {
                    logger.LogDebug("Demuxer: outbound response with non-int id discarded");
                    return;
                }
                if (waiters.TryGetValue(id, out var tcs))
                {
                    // TrySetResult: if the waiter already cancelled (timeout fired) this is a no-op.
                    tcs.TrySetResult(line);
                }
                else
                {
                    // Late response after the client's request already timed out -- drop.
                    logger.LogDebug("Demuxer: response for unknown id {Id} discarded (late)", id);
                }
            }
            catch (JsonException)
            {
                logger.LogDebug("Demuxer: unparseable outbound frame discarded");
            }
        }

        private static async Task BroadcastSseAsync(
            string jsonFrame,
            ConcurrentDictionary<Guid, SseSubscriber> sseSubscribers,
            ILogger logger,
            CancellationToken ct)
        {
            if (sseSubscribers.IsEmpty)
            {
                return;
            }

            // SSE wire format: an "event:" line and a "data:" line, terminated by a blank line.
            // The JSON frame is single-line by construction (ReadLineAsync stops at '\n'), so it
            // safely fits in one data: field with no need to split.
            var payload = Encoding.UTF8.GetBytes("event: message\ndata: " + jsonFrame + "\n\n");

            foreach (var kv in sseSubscribers)
            {
                var sub = kv.Value;
                try
                {
                    await sub.WriteAsync(payload, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "SSE: write failed for subscriber {Id}; removing", sub.Id);
                    if (sseSubscribers.TryRemove(sub.Id, out var removed))
                    {
                        removed.Dispose();
                    }
                }
            }
        }

        private static async Task HandleSseAsync(
            HttpListenerContext context,
            ConcurrentDictionary<Guid, SseSubscriber> sseSubscribers,
            string? apiKey,
            ILogger logger,
            CancellationToken ct)
        {
            var request = context.Request;
            var response = context.Response;

            if (!string.IsNullOrEmpty(apiKey))
            {
                var presented = request.Headers["X-API-Key"];
                if (string.IsNullOrEmpty(presented) || !string.Equals(presented, apiKey, StringComparison.Ordinal))
                {
                    await WriteStatusAsync(response, 401, "Unauthorized").ConfigureAwait(false);
                    try { response.OutputStream.Close(); } catch { }
                    try { response.Close(); } catch { }
                    return;
                }
            }

            response.StatusCode = 200;
            response.ContentType = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.KeepAlive = true;
            // Unknown body length: stream with chunked transfer encoding so we can keep writing
            // as long as the client stays connected.
            response.SendChunked = true;

            var sub = new SseSubscriber(response);
            sseSubscribers[sub.Id] = sub;
            logger.LogInformation("SSE: subscriber {Id} connected ({Count} active)", sub.Id, sseSubscribers.Count);

            CancellationTokenRegistration ctReg = default;
            try
            {
                // Prime the stream so the headers flush and the client sees the connection open
                // even before any notifications arrive.
                var hello = Encoding.UTF8.GetBytes(": connected\n\n");
                await sub.WriteAsync(hello, ct).ConfigureAwait(false);

                // Server shutdown signals the subscriber's Closed TCS so this handler unblocks.
                ctReg = ct.Register(() => sub.Closed.TrySetResult(true));

                // Park here until shutdown or a broadcast write fails and disposes us.
                await sub.Closed.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "SSE: subscriber {Id} setup/wait failed", sub.Id);
            }
            finally
            {
                ctReg.Dispose();
                if (sseSubscribers.TryRemove(sub.Id, out var removed))
                {
                    removed.Dispose();
                }
                logger.LogInformation("SSE: subscriber {Id} disconnected ({Count} active)", sub.Id, sseSubscribers.Count);
            }
        }

        // Holds one HTTP listener response that's been hijacked as an SSE stream. Writes are
        // serialized through a per-subscriber semaphore -- BroadcastSseAsync runs from the demuxer
        // (single threaded today) but the heartbeat / initial prime could otherwise race.
        private sealed class SseSubscriber : IDisposable
        {
            private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
            private int _disposed;

            public Guid Id { get; } = Guid.NewGuid();
            public HttpListenerResponse Response { get; }
            public TaskCompletionSource<bool> Closed { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public SseSubscriber(HttpListenerResponse response)
            {
                Response = response;
            }

            public async Task WriteAsync(byte[] payload, CancellationToken ct)
            {
                await _writeLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await Response.OutputStream.WriteAsync(payload, 0, payload.Length, ct).ConfigureAwait(false);
                    await Response.OutputStream.FlushAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    _writeLock.Release();
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { Response.OutputStream.Close(); } catch { }
                try { Response.Close(); } catch { }
                Closed.TrySetResult(true);
                _writeLock.Dispose();
            }
        }

        private static async Task<string?> ReadLineAsync(PipeReader reader, CancellationToken ct)
        {
            // Manual scan: ReadOnlySequence<byte>.PositionOf / ToArray aren't on the
            // netstandard2.0 surface that net48 sees from System.Memory 4.6.0. The
            // scan itself touches Span<T> (a ref struct), so it lives in a non-async
            // helper -- C# 12 doesn't allow ref structs inside async methods.
            using (var line = new MemoryStream())
            {
                while (true)
                {
                    var result = await reader.ReadAsync(ct).ConfigureAwait(false);
                    var buffer = result.Buffer;

                    if (TryScanLine(buffer, line, out var consumed))
                    {
                        reader.AdvanceTo(consumed);
                        var raw = line.ToArray();
                        return Encoding.UTF8.GetString(raw).TrimEnd('\r');
                    }

                    if (result.IsCompleted)
                    {
                        reader.AdvanceTo(buffer.End);
                        return null;
                    }

                    reader.AdvanceTo(buffer.End);
                }
            }
        }

        private static bool TryScanLine(System.Buffers.ReadOnlySequence<byte> buffer, MemoryStream line, out System.SequencePosition consumed)
        {
            long bufferOffset = 0;
            foreach (var segment in buffer)
            {
                var span = segment.Span;
                for (int i = 0; i < span.Length; i++)
                {
                    byte b = span[i];
                    if (b == (byte)'\n')
                    {
                        consumed = buffer.GetPosition(bufferOffset + i + 1);
                        return true;
                    }
                    line.WriteByte(b);
                }
                bufferOffset += span.Length;
            }
            consumed = buffer.End;
            return false;
        }

        private static Task WriteStatusAsync(HttpListenerResponse response, int statusCode, string message)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes("{\"error\":\"" + EscapeJson(message) + "\"}");
            response.ContentLength64 = bytes.LongLength;
            return response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private static string EscapeJson(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
