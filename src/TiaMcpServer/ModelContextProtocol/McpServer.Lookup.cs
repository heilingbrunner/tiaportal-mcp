using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Getting started and getting un-stuck: one call to reach a usable project, and one call to
    /// turn a bare object name into the root-relative path every other tool expects.
    ///
    /// Callers: the MCP host, through tool registration. Affected API: none existing - both
    /// tools are new. Reads and writes no data files.
    ///
    /// Neither tool modifies the project, so neither is gated behind '--allow-write'. OpenTiaProject
    /// changes which project is open, which is why it keeps 'Destructive = true' like OpenProject.
    /// </summary>
    public static partial class McpServer
    {
        #region lookup

        [McpServerTool(Name = "ResolveObjectPath", Title = "Resolve a name to its path", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Turn a bare or partial object name into the root-relative path the other tools need, searching program blocks, PLC data types, tags, tag tables, watch tables and external sources. Exact matches win; substring matches are only reported when nothing matches exactly")]
        public static ResponseResolveObjectPath ResolveObjectPath(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: the object name to look for, e.g. 'FC_Block_1'. A full path may be passed; only its last segment is matched")] string name,
            [Description("kind: restrict the search to 'block', 'type', 'tag', 'tagTable', 'watchTable' or 'source'. Default 'any' searches all of them")] string kind = "any")
        {
            try
            {
                var matches = Portal.ResolveObjectPath(softwarePath, name, kind);

                return new ResponseResolveObjectPath
                {
                    Message = matches.Count switch
                    {
                        0 => $"No object named '{name}' found in '{softwarePath}'",
                        1 => $"'{name}' resolves to '{matches[0].Path}' ({matches[0].Kind})",
                        _ => $"{matches.Count} objects match '{name}' in '{softwarePath}'"
                    },
                    Items = matches.Select(m => new ResponseObjectMatch
                    {
                        Kind = m.Kind,
                        Name = m.Name,
                        Path = m.Path
                    }).ToList(),
                    Meta = Ok(new JsonObject { ["matches"] = matches.Count, ["kind"] = kind })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error resolving '{name}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "OpenTiaProject", Title = "Connect and open a project", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Connect to TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the Connect then OpenProject then GetProjectTree sequence")]
        public static ResponseOpenTiaProject OpenTiaProject(
            [Description("path: full path of the .apXX project or .alsXX session file on the machine running this server")] string path)
        {
            try
            {
                var connected = Portal.IsConnected();

                if (!connected && !Portal.ConnectPortal())
                {
                    throw new McpException(
                        "Failed to connect to TIA Portal. Run the 'Doctor' tool to check the installation and user group membership.");
                }

                // Reuse the existing tool rather than duplicating its extension validation and
                // project/session branching; it also closes whatever was open first.
                var opened = OpenProject(path);

                var softwarePaths = CollectSoftwarePaths();

                return new ResponseOpenTiaProject
                {
                    Message = $"{opened.Message}. PLC software: " +
                              (softwarePaths.Count > 0 ? string.Join(", ", softwarePaths) : "none found"),
                    ProjectPath = path,
                    WasAlreadyConnected = connected,
                    SoftwarePaths = softwarePaths,
                    Tree = Portal.GetProjectTree(),
                    Meta = Ok(new JsonObject { ["softwareCount"] = softwarePaths.Count })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening '{path}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// PLC software paths from the project tree, so the caller can pass one straight into the
        /// software tools instead of parsing the ASCII tree.
        /// </summary>
        private static List<string> CollectSoftwarePaths()
        {
            try
            {
                return Portal.GetSoftwarePaths();
            }
            catch (Exception)
            {
                // Orientation is a convenience; a project that opened but cannot be walked yet
                // should still report success for the open itself.
                return new List<string>();
            }
        }

        #endregion
    }
}
