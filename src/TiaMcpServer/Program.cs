using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            var options = CliOptions.ParseArgs(args);

            Engineering.TiaMajorVersion = options.TiaMajorVersion ?? 21;

            if (Engineering.TiaMajorVersion >= 20)
            {
                Openness.Initialize(Engineering.TiaMajorVersion);
            }

            // Fallback: the Siemens resolver does not cover every Siemens.Engineering.* satellite assembly.
            AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;

            // Ensure user is in user group 'Siemens TIA Openness'
            if (await Openness.IsUserInGroup())
            {
                await RunStdioHost(options);
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
                    .AddMcpServer(serverOptions =>
                    {
                        serverOptions.ServerInfo = new global::ModelContextProtocol.Protocol.Implementation
                        {
                            Name = "TiaMcpServer",
                            Title = "TIA Portal MCP Server",
                            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"
                        };

                        serverOptions.ServerInstructions =
                            "Exposes Siemens TIA Portal via Openness. Call 'Connect' first, then 'OpenProject' with an " +
                            "absolute .apXX project or .alsXX session path. Use 'GetProjectTree' or 'GetSoftwareTree' to " +
                            "discover the path strings that the other tools expect. Export and import tools operate on " +
                            "the local file system of the machine running this server.";
                    })
                    .WithStdioServerTransport()
                    .WithTools((IEnumerable<Type>)new[] { typeof(McpServer) })
                    .WithPrompts((IEnumerable<Type>)new[] { typeof(McpPrompts) });

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
    }
}
