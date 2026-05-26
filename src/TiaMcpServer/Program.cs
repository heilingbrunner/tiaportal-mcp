using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
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

                var transportGate = new SemaphoreSlim(1, 1);
                var hostTask = host.RunAsync(cts.Token);
                var listenerTask = AcceptLoopAsync(listener, inboundPipe.Writer, outboundPipe.Reader, transportGate, options.HttpApiKey, logger, cts.Token);

                try
                {
                    await Task.WhenAny(hostTask, listenerTask);
                }
                finally
                {
                    cts.Cancel();
                    try { listener.Stop(); } catch { }
                    listener.Close();

                    // Closing the inbound writer signals EOF to the MCP server so it shuts down.
                    inboundPipe.Writer.Complete();
                    try { await hostTask; } catch (OperationCanceledException) { }
                    try { await listenerTask; } catch (OperationCanceledException) { }

                    Console.CancelKeyPress -= cancelHandler;
                }
            }
        }

        private static async Task AcceptLoopAsync(
            HttpListener listener,
            PipeWriter inboundWriter,
            PipeReader outboundReader,
            SemaphoreSlim gate,
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

                _ = Task.Run(() => HandleRequestAsync(context, inboundWriter, outboundReader, gate, apiKey, logger, ct), ct);
            }
        }

        private static async Task HandleRequestAsync(
            HttpListenerContext context,
            PipeWriter inboundWriter,
            PipeReader outboundReader,
            SemaphoreSlim gate,
            string? apiKey,
            ILogger logger,
            CancellationToken ct)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
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

                bool hasId;
                try
                {
                    hasId = JsonRequestHasId(body);
                }
                catch (JsonException)
                {
                    await WriteStatusAsync(response, 400, "Invalid JSON").ConfigureAwait(false);
                    return;
                }

                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var payload = Encoding.UTF8.GetBytes(body.Trim() + "\n");
                    await inboundWriter.WriteAsync(payload, ct).ConfigureAwait(false);
                    await inboundWriter.FlushAsync(ct).ConfigureAwait(false);

                    if (!hasId)
                    {
                        // Notification: MCP server will not produce a response.
                        await WriteStatusAsync(response, 202, "Accepted").ConfigureAwait(false);
                        return;
                    }

                    // Bound the read on a linked CTS so a hung SDK cannot pin the gate forever.
                    // Outer ct stays the cancellation source for shutdown; CancelAfter bolts on
                    // the per-request budget. When the linked token fires alone, we surface 504.
                    using (var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        requestCts.CancelAfter(TimeSpan.FromSeconds(ResponseTimeoutSeconds));
                        try
                        {
                            var responseLine = await ReadJsonRpcResponseAsync(outboundReader, requestCts.Token).ConfigureAwait(false);
                            if (responseLine == null)
                            {
                                await WriteStatusAsync(response, 500, "MCP server closed the response stream").ConfigureAwait(false);
                                return;
                            }

                            var bytes = Encoding.UTF8.GetBytes(responseLine);
                            response.StatusCode = 200;
                            response.ContentType = "application/json";
                            response.ContentLength64 = bytes.LongLength;
                            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            // Per-request budget exhausted; server shutdown was NOT requested.
                            logger.LogWarning("Request exceeded {Seconds}s response budget; returning 504", ResponseTimeoutSeconds);
                            await WriteStatusAsync(response, 504, $"Gateway Timeout: MCP server did not respond within {ResponseTimeoutSeconds}s").ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    gate.Release();
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

        private static bool JsonRequestHasId(string body)
        {
            using (var doc = JsonDocument.Parse(body))
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("Expected JSON-RPC object");
                }
                return doc.RootElement.TryGetProperty("id", out _);
            }
        }

        private static async Task<string?> ReadJsonRpcResponseAsync(PipeReader reader, CancellationToken ct)
        {
            // The MCP server may emit notifications (no "id") interleaved with the actual
            // response. Skip past notifications until we see a line carrying an "id" field.
            while (true)
            {
                var line = await ReadLineAsync(reader, ct).ConfigureAwait(false);
                if (line == null)
                {
                    return null;
                }

                try
                {
                    using (var doc = JsonDocument.Parse(line))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Object
                            && doc.RootElement.TryGetProperty("id", out _))
                        {
                            return line;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Not parseable as a JSON-RPC frame; treat as noise and keep reading.
                }
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
