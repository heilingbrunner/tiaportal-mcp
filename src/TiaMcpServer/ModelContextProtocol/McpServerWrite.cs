using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// The project-mutating MCP tools (create, rename, delete, import into the project).
    ///
    /// Callers: registered by Program.BuildToolTypes() only when '--allow-write' was passed, and
    /// invoked directly by the write test classes. Affected API: none existing. Data: returns the
    /// shared ResponseCreated / ResponseDeleted / ResponseRenamed / ResponseImported /
    /// ResponseGenerateBlocks DTOs. The import tools read a caller-supplied file; nothing here
    /// writes files. Project changes live in memory until SaveProject (or SaveSession).
    ///
    /// Conventions, enforced by the Guarded helper below:
    ///  - WritePolicy.EnsureEnabled runs first, because these are public static methods that the
    ///    MSTest suite calls directly, bypassing tool registration.
    ///  - A PortalException is surfaced with its own guidance message; anything else is wrapped.
    ///  - Destructive = true; Idempotent = true only for renames and deletes-by-name.
    ///
    /// Filesystem-only exports stay in McpServer: they never modify the project.
    /// </summary>
    [McpServerToolType]
    public static partial class McpServerWrite
    {
        #region plumbing

        private static Portal Portal => McpServer.Portal;

        /// <summary>
        /// Reminds the caller that Openness edits are in-memory, naming the right save tool for
        /// the current mode: a multiuser local session saves through SaveSession, not SaveProject.
        /// </summary>
        private static string SaveHint =>
            Portal.IsLocalSession
                ? "The change is in memory; call 'SaveSession' to persist it."
                : "The change is in memory; call 'SaveProject' to persist it.";

        private static T Guarded<T>(string toolName, Func<T> body)
        {
            WritePolicy.EnsureEnabled(toolName);

            try
            {
                return body();
            }
            catch (PortalException pex)
            {
                // PortalException messages are already written for the caller (what went wrong
                // and which tool lists the valid paths), so they pass through unchanged.
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error in '{toolName}': {ex.Message}", ex);
            }
        }

        private static JsonObject OkMeta() => new JsonObject
        {
            ["timestamp"] = DateTime.Now,
            ["success"] = true,
            ["pendingSave"] = true
        };

        private static ResponseCreated Created(string kind, string name, string path) => new ResponseCreated
        {
            Kind = kind,
            Name = name,
            Path = path,
            Message = $"{kind} '{name}' created. {SaveHint}",
            Meta = OkMeta()
        };

        private static ResponseDeleted Deleted(string kind, string path) => new ResponseDeleted
        {
            Kind = kind,
            Path = path,
            Message = $"{kind} '{path}' deleted. {SaveHint}",
            Meta = OkMeta()
        };

        private static ResponseRenamed Renamed(string kind, string oldPath, string newName, string newPath) => new ResponseRenamed
        {
            Kind = kind,
            OldPath = oldPath,
            NewName = newName,
            NewPath = newPath,
            Message = $"{kind} '{oldPath}' renamed to '{newName}'. {SaveHint}",
            Meta = OkMeta()
        };

        private static ResponseImported Imported(string kind, string groupPath, string importPath) => new ResponseImported
        {
            Kind = kind,
            GroupPath = groupPath,
            ImportPath = importPath,
            Message = $"{kind} imported from '{importPath}' into '{groupPath}'. {SaveHint}",
            Meta = OkMeta()
        };

        /// <summary>Replaces the last segment of a path, for reporting a rename's new path.</summary>
        private static string ReplaceLeaf(string path, string newName)
        {
            var trimmed = (path ?? string.Empty).Trim('/');
            var index = trimmed.LastIndexOf('/');

            return index < 0 ? newName : trimmed.Substring(0, index + 1) + newName;
        }

        private static string Join(string groupPath, string name) =>
            string.IsNullOrEmpty(groupPath) ? name : $"{groupPath}/{name}";

        #endregion
    }
}
