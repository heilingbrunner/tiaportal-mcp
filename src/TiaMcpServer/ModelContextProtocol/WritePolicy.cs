using ModelContextProtocol;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Gates the project-mutating tools behind the '--allow-write' command line flag.
    ///
    /// Callers: Program.cs (sets AllowWrite from CliOptions and conditionally registers the
    /// write tool type) and every tool in McpServerWrite.cs. Affected API: none existing - this
    /// is a new type and changes no current signature. Reads/writes no data files.
    ///
    /// Two mechanisms guard writes, deliberately:
    ///  - Program.cs registers the McpServerWrite tool type only when AllowWrite is set, which
    ///    is what keeps the destructive tools out of 'tools/list' entirely.
    ///  - EnsureEnabled is the first statement of every write tool, because those tools are
    ///    public static methods that the MSTest suite invokes directly, bypassing registration.
    ///
    /// Filesystem-only exports are not gated: they never modify the TIA project.
    /// </summary>
    public static class WritePolicy
    {
        public static bool AllowWrite { get; set; }

        public static void EnsureEnabled(string toolName)
        {
            if (!AllowWrite)
            {
                throw new McpException(
                    $"Tool '{toolName}' modifies the TIA Portal project and is disabled. " +
                    "Restart the MCP server with '--allow-write' to enable write operations.");
            }
        }
    }
}
