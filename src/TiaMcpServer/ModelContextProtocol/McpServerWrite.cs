using ModelContextProtocol.Server;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// The project-mutating MCP tools (create, rename, delete, import into the project).
    ///
    /// Callers: registered by Program.BuildToolTypes() only when '--allow-write' was passed, and
    /// invoked directly by the write test classes. Affected API: none existing. Reads/writes no
    /// data files itself; the tools it will hold mutate the open TIA Portal project in memory.
    ///
    /// Conventions for every tool added here:
    ///  - WritePolicy.EnsureEnabled(nameof(Tool)) as the first statement, because the MSTest
    ///    suite calls these methods directly and bypasses tool registration.
    ///  - Destructive = true; Idempotent = true only for renames and deletes-by-name.
    ///  - The Message must state that changes stay in memory until SaveProject / SaveSession.
    ///  - Reuse ResponseCreated / ResponseDeleted / ResponseRenamed rather than minting a DTO
    ///    per operation.
    ///
    /// Filesystem-only exports stay in McpServer: they never modify the project.
    /// </summary>
    [McpServerToolType]
    public static partial class McpServerWrite
    {
        // Tools are added here in Phase 6 of the PLC-software coverage work.
    }
}
