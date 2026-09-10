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

            // Set before the --doctor branch below, which reports the write mode, and before the
            // host registers its tool types.
            WritePolicy.AllowWrite = options.AllowWrite;

            if (Engineering.TiaMajorVersion >= 20)
            {
                Openness.Initialize(Engineering.TiaMajorVersion);
            }

            // Fallback: the Siemens resolver does not cover every Siemens.Engineering.* satellite assembly.
            AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;

            // '--doctor' only reports the environment - it must run before the user group gate below,
            // because a missing group membership is one of the things it is meant to diagnose.
            if (options.Doctor)
            {
                RunDoctor();
                return;
            }

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

        /// <summary>
        /// Prints the environment diagnostics of the '--doctor' command. Read-only: it neither
        /// connects to TIA Portal nor changes user group membership.
        /// </summary>
        public static void RunDoctor()
        {
            try
            {
                // Fully qualified: 'Diagnostics' alone would collide with the System.Diagnostics namespace.
                var report = TiaMcpServer.Siemens.Diagnostics.Run(new Portal(), WritePolicy.AllowWrite);

                Console.WriteLine(report.Text);
            }
            catch (Exception ex)
            {
                // A diagnostics command must report a problem, not crash with a stack trace.
                Console.Error.WriteLine($"Diagnose failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }

        /// <summary>
        /// The read-only tools are always registered. The project-mutating tools live in a
        /// separate tool type that is only registered with '--allow-write', which is what keeps
        /// them out of 'tools/list' rather than merely refusing them when called.
        /// </summary>
        private static IEnumerable<Type> BuildToolTypes()
        {
            var toolTypes = new List<Type> { typeof(McpServer) };

            if (WritePolicy.AllowWrite)
            {
                toolTypes.Add(typeof(McpServerWrite));
            }

            return toolTypes;
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
                            "the local file system of the machine running this server." +
                            (WritePolicy.AllowWrite
                                ? " Write mode is enabled: tools that create, rename or delete project objects are " +
                                  "available. Their changes stay in memory until 'SaveProject' (or 'SaveSession')."
                                : string.Empty);
                    })
                    .WithStdioServerTransport()
                    .WithTools(BuildToolTypes())
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
