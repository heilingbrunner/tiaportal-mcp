using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerToolType]
    public static partial class McpServer
    {
        private static IServiceProvider? _services;

        private static Portal? _portal;

        public static ILogger? Logger { get; set; }

        public static Portal Portal
        {
            get
            {
                if (_services !=null)
                {
                    return _services.GetRequiredService<Portal>();
                }
                else
                {
                    if (_portal == null)
                    {
                        _portal = new Portal();
                    }
                    return _portal;
                }
            }
            set
            {
                _portal = value ?? throw new ArgumentNullException(nameof(value), "Portal cannot be null");
            }
        }

        public static void SetServiceProvider(IServiceProvider services)
        {
            _services = services;
        }

        #region portal

        [McpServerTool(Name = "Connect", Title = "Connect to TIA Portal", Destructive = false, Idempotent = true, OpenWorld = false),
         Description("Connect to TIA-Portal and return its 'portalId'. Without 'portalId' it attaches to the only running instance, or starts one if none is running; with several running it fails and lists them")]
        public static ResponseConnect Connect(
            [Description("portalId: process id of the TIA Portal instance to attach to (see 'GetPortals'). Leave empty when at most one instance is running")] int? portalId = null)
        {
            Logger?.LogInformation("Connecting to TIA Portal...");

            try
            {
                var attachedId = Portal.AttachPortal(portalId);

                return new ResponseConnect
                {
                    Message = $"Connected to TIA-Portal instance {attachedId}",
                    PortalId = attachedId,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(PortalSelection.Describe(pex), pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting to TIA-Portal: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "Disconnect", Title = "Disconnect from TIA Portal", Destructive = false, Idempotent = true, OpenWorld = false), Description("Disconnect from TIA-Portal")]
        public static ResponseDisconnect Disconnect()
        {
            try
            {
                if (Portal.DisconnectPortal())
                {
                    return new ResponseDisconnect
                    {
                        Message = "Disconnected from TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed disconnecting from TIA-Portal");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error disconnecting from TIA-Portal: {ex.Message}", ex);
            }
        }

        [PortalIndependent]
        [McpServerTool(Name = "GetPortals", Title = "List TIA Portal instances", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the running TIA-Portal instances with their 'portalId', open project path and whether this server is attached. Use it to choose the 'portalId' when more than one instance is running")]
        public static ResponsePortals GetPortals()
        {
            try
            {
                var portals = Portal.GetPortals();

                return new ResponsePortals
                {
                    Message = $"{portals.Count} TIA-Portal instance(s) running",
                    Portals = portals,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error listing TIA-Portal instances: {ex.Message}", ex);
            }
        }

        #endregion

        #region state

        [McpServerTool(Name = "GetState", Title = "Get server state", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get the state of the TIA-Portal MCP server")]
        public static ResponseState GetState()
        {
            try
            {
                var state = Portal.GetState();

                if (state != null)
                {
                    return new ResponseState
                    {
                        Message = "TIA-Portal MCP server state retrieved",
                        PortalId = state.PortalId,
                        IsConnected = state.IsConnected,
                        Project = state.Project,
                        Session = state.Session,
                        AllowWrite = WritePolicy.AllowWrite,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to retrieve TIA-Portal MCP server state");
                }
                

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving TIA-Portal MCP server state: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "Doctor", Title = "Diagnose the TIA Portal environment", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Diagnose the TIA-Portal environment: connection, open project, active and installed TIA-Portal versions, Openness user group membership")]
        public static ResponseDoctor Doctor()
        {
            Logger?.LogInformation("Running TIA Portal diagnostics...");

            try
            {
                // Fully qualified: 'Diagnostics' alone would collide with the System.Diagnostics namespace.
                var report = TiaMcpServer.Siemens.Diagnostics.Run(Portal, WritePolicy.AllowWrite);

                return new ResponseDoctor
                {
                    Message = "TIA-Portal environment diagnosed",
                    Report = report.Text,
                    ServerVersion = report.ServerVersion,
                    IsConnected = report.IsConnected,
                    ActiveTiaMajorVersion = report.ActiveTiaMajorVersion,
                    ProjectName = report.ProjectName,
                    ProjectPath = report.ProjectPath,
                    IsUserInGroup = report.IsUserInGroup,
                    AllowWrite = report.AllowWrite,
                    Installations = report.Installations
                        .Select(i => new ResponseTiaInstallation
                        {
                            MajorVersion = i.MajorVersion,
                            InstallPath = i.InstallPath,
                            EngineeringExists = i.EngineeringExists,
                            PortalExeExists = i.PortalExeExists
                        })
                        .ToList(),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error diagnosing the TIA-Portal environment: {ex.Message}", ex);
            }
        }

        #endregion

        #region project/session

        [McpServerTool(Name = "GetProject", Title = "Get open project", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get open local project/session")]
        public static ResponseGetProjects GetProjects()
        {
            try
            {
                var list = Portal.GetProjects();

                list.AddRange(Portal.GetSessions());

                var responseList = new List<ResponseProjectInfo>();
                foreach (var project in list)
                {
                    var attributes = Helper.GetAttributeList(project);

                    if (project != null)
                    {
                        responseList.Add(new ResponseProjectInfo
                        {
                            Name = project.Name,
                            Attributes = attributes
                        });
                    }
                }

                return new ResponseGetProjects
                {
                    Message = "Open projects and sessions retrieved",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving open projects: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "OpenProject", Title = "Open project/session", Destructive = false, Idempotent = true, OpenWorld = false), Description("Open a TIA-Portal local project/session")]
        public static ResponseOpenProject OpenProject(
            [Description("path: defines the path where to the project/session")] string path)
        {
            try
            {
                Portal.CloseProject();

                // get project extension
                string extension = Path.GetExtension(path).ToLowerInvariant();

                // use regex to check if extension is .ap\d+ or .als\d+
                if (!Regex.IsMatch(extension, @"^\.ap\d+$") &&
                    !Regex.IsMatch(extension, @"^\.als\d+$"))
                {
                    throw new McpException("Invalid project file extension. Use .apXX for projects or .alsXX for sessions, where XX=18,19,20,....");
                }

                bool success = false;

                if (extension.StartsWith(".ap"))
                {
                    success = Portal.OpenProject(path);
                }
                if (extension.StartsWith(".als"))
                {
                    success = Portal.OpenSession(path);
                }

                if (success)
                {
                    return new ResponseOpenProject
                    {
                        Message = $"Project '{path}' opened",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed to open project '{path}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening project '{path}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "SaveProject", Title = "Save project", Destructive = true, Idempotent = true, OpenWorld = false), Description("Save the current TIA-Portal local project/session")]
        public static ResponseSaveProject SaveProject()
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    if (Portal.SaveSession())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local session saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save local session");
                    }
                }
                else
                {
                    if (Portal.SaveProject())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local project saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save project");
                    }
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "SaveAsProject", Title = "Save project as", Destructive = true, Idempotent = true, OpenWorld = false), Description("Save current TIA-Portal project/session with a new name")]
        public static ResponseSaveAsProject SaveAsProject(
            [Description("newProjectPath: defines the new path where to save the project")] string newProjectPath)
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    throw new McpException($"Cannot save local session as '{newProjectPath}'");
                }
                else
                {
                    if (Portal.SaveAsProject(newProjectPath))
                    {
                        return new ResponseSaveAsProject
                        {
                            Message = $"Local project saved as '{newProjectPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException($"Failed saving local project as '{newProjectPath}'");
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session as '{newProjectPath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "CloseProject", Title = "Close project", Destructive = true, Idempotent = true, OpenWorld = false), Description("Close the current TIA-Portal project/session")]
        public static ResponseCloseProject CloseProject()
        {
            try
            {
                bool success;

                if (Portal.IsLocalSession)
                {
                    success = Portal.CloseSession();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local session closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing local session");
                    }
                }
                else
                {
                    success = Portal.CloseProject();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local project closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing project");
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error closing local project/session: {ex.Message}", ex);
            }
        }

        #endregion

        #region devices

        [McpServerTool(Name = "GetProjectTree", Title = "Get project tree", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get project structure as a tree view on current local project/session")]
        public static ResponseProjectTree GetProjectTree()
        {
            try
            {
                var tree = Portal.GetProjectTree();

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseProjectTree
                    {
                        Message = "Project tree retrieved",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed retrieving project tree");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving project tree: {ex.Message}", ex);
            }
        }

        #endregion

        #region lookup

        [McpServerTool(Name = "OpenTiaProject", Title = "Connect/open a project", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Connect to TIA Portal if not already connected, open the given project or session, and return the 'portalId' plus the device and PLC software paths the other tools need. Replaces the Connect then OpenProject then GetProjectTree sequence")]
        public static async Task<ResponseOpenTiaProject> OpenTiaProject(
            [Description("path: full path of the .apXX project or .alsXX session file on the machine running this server")] string path,
            [Description("portalId: process id of the TIA Portal instance to open it in (see 'GetPortals'). Leave empty when at most one instance is running")] int? portalId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                int attachedId;
                bool wasAttached;

                try
                {
                    // Attaching is unscoped, as in 'Connect': the instance may not be attached yet,
                    // which is why this tool declares 'portalId' itself and bypasses the filter.
                    attachedId = Portal.AttachPortal(portalId, out wasAttached);
                }
                catch (Exception ex) when (ex is not PortalException)
                {
                    throw new McpException(
                        "Failed to connect to TIA Portal. Run the 'Doctor' tool to check the installation and user group membership.", ex);
                }

                // Everything after attaching runs scoped to that instance and under its lock.
                return await Portal.RunScopedAsync(
                    attachedId,
                    () => new ValueTask<ResponseOpenTiaProject>(OpenAndDescribe(path, attachedId, wasAttached)),
                    cancellationToken);
            }
            catch (PortalException pex)
            {
                throw new McpException(PortalSelection.Describe(pex), pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening '{path}': {ex.Message}", ex);
            }
        }

        private static ResponseOpenTiaProject OpenAndDescribe(string path, int portalId, bool wasAttached)
        {
            // Reuse the existing tool rather than duplicating its extension validation and
            // project/session branching; it also closes whatever was open first.
            var opened = OpenProject(path);

            var softwarePaths = CollectSoftwarePaths();

            return new ResponseOpenTiaProject
            {
                Message = $"{opened.Message} in TIA-Portal instance {portalId}. PLC software: " +
                          (softwarePaths.Count > 0 ? string.Join(", ", softwarePaths) : "none found"),
                PortalId = portalId,
                ProjectPath = path,
                WasAlreadyConnected = wasAttached,
                SoftwarePaths = softwarePaths,
                Tree = Portal.GetProjectTree(),
                Meta = Ok(new JsonObject { ["softwareCount"] = softwarePaths.Count })
            };
        }

        #endregion

        // From the former McpServer.Preview.cs:
        // Looking before writing.
        //
        // Callers: the MCP host, through tool registration. Affected API: none - PreviewImport is
        // new and no write tool changed. File I/O: reads the caller's import directory; writes
        // nothing, and never touches the project.
        //
        // This is the read-only half of write safety; the other half is the transaction wrapper in
        // Portal.cs (InTransaction), which every write tool now runs inside. It is one tool rather
        // than a 'dryRun' flag on all 39 write tools: the flag would have changed the execution
        // path of every write for a report that only imports really need, and imports are where
        // the collisions actually happen.
        //
        // What it can and cannot know: the object name is taken from the file name, which is how
        // every exporter in this server names its output. A hand-edited file whose content
        // declares a different name than its file name would be reported under the file name.

        #region preview

        [McpServerTool(Name = "PreviewImport", Title = "Preview import", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'")]
        public static ResponseImportPreview PreviewImport(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: directory holding the files to import (.s7dcl source documents or .xml)")] string importPath,
            [Description("kind: what the files contain - 'type' for PLC data types, 'block' for program blocks")] string kind,
            [Description("groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted")] string groupPath = "")
        {
            try
            {
                if (!Directory.Exists(importPath))
                {
                    throw new McpException($"Import directory '{importPath}' does not exist.");
                }

                var isType = kind.Equals("type", StringComparison.OrdinalIgnoreCase);

                if (!isType && !kind.Equals("block", StringComparison.OrdinalIgnoreCase))
                {
                    throw new McpException($"Unknown kind '{kind}'. Use 'type' or 'block'.");
                }

                var names = Directory
                    .GetFiles(importPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => PreviewExtensions.Contains(Path.GetExtension(f)))
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var target = string.IsNullOrEmpty(groupPath) ? "the root" : groupPath;
                var items = new List<ResponseImportPreviewItem>();

                foreach (var name in names)
                {
                    var existing = Portal.ResolveObjectPath(softwarePath, name, isType ? "type" : "block")
                        .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        items.Add(new ResponseImportPreviewItem { Name = name, Effect = "create", TargetPath = Join(groupPath, name) });

                        continue;
                    }

                    var sameGroup = string.Equals(
                        Parent(existing.Path),
                        StripLeadingSystemFolder(groupPath),
                        StringComparison.OrdinalIgnoreCase);

                    items.Add(new ResponseImportPreviewItem
                    {
                        Name = name,
                        Effect = sameGroup ? "overwrite" : (isType ? "conflict" : "overwrite-elsewhere"),
                        TargetPath = Join(groupPath, name),
                        ExistingPath = existing.Path,
                        Note = sameGroup
                            ? "Already exists in the target group; importOption 'Override' replaces it."
                            : isType
                                ? "A PLC data type name must be unique across the whole PLC. This import fails even with " +
                                  $"'Override' - target '{Parent(existing.Path)}' instead, or rename the type."
                                : $"A block of this name already exists at '{existing.Path}'; importing here may collide on the block number."
                    });
                }

                var creates = items.Count(i => i.Effect == "create");
                var overwrites = items.Count(i => i.Effect == "overwrite");
                var conflicts = items.Count(i => i.Effect == "conflict" || i.Effect == "overwrite-elsewhere");

                return new ResponseImportPreview
                {
                    Message = $"Importing '{importPath}' into {target} would create {creates}, overwrite {overwrites}, " +
                              $"and hit {conflicts} conflict(s). Nothing was changed.",
                    Items = items,
                    CreateCount = creates,
                    OverwriteCount = overwrites,
                    ConflictCount = conflicts,
                    Meta = Ok(new JsonObject
                    {
                        ["files"] = names.Count,
                        ["create"] = creates,
                        ["overwrite"] = overwrites,
                        ["conflict"] = conflicts
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error previewing the import of '{importPath}': {ex.Message}", ex);
            }
        }

        /// <summary>File kinds an export of this server produces, and therefore an import consumes.</summary>
        private static readonly HashSet<string> PreviewExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".s7dcl", ".xml" };

        private static string Join(string groupPath, string name) =>
            string.IsNullOrEmpty(groupPath) ? name : $"{groupPath.TrimEnd('/')}/{name}";

        private static string Parent(string path)
        {
            var slash = path.LastIndexOf('/');

            return slash < 0 ? string.Empty : path.Substring(0, slash);
        }

        /// <summary>
        /// A groupPath copied from a preservePath export starts with the localised system folder
        /// ('PLC data types', 'Program blocks'), which the resolved paths do not carry.
        /// </summary>
        private static string StripLeadingSystemFolder(string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath) || !groupPath.Contains("/"))
            {
                return string.Empty;
            }

            return groupPath.Substring(groupPath.IndexOf('/') + 1);
        }

        #endregion

        // From the former McpServerWrite.cs:
        // The project-mutating MCP tools (create, rename, delete, import into the project).
        //
        // Callers: registered by Program.BuildTools() only when '--allow-write' was passed, and
        // invoked directly by the write test classes. Affected API: none existing. Data: returns the
        // shared ResponseCreated / ResponseDeleted / ResponseRenamed / ResponseImported /
        // ResponseGenerateBlocks DTOs. The import tools read a caller-supplied file; nothing here
        // writes files. Project changes live in memory until SaveProject (or SaveSession).
        //
        // Conventions, enforced by the Guarded helper below:
        // - WritePolicy.EnsureEnabled runs first, because these are public static methods that the
        // MSTest suite calls directly, bypassing tool registration.
        // - A PortalException is surfaced with its own guidance message; anything else is wrapped.
        // - Destructive = true; Idempotent = true only for renames and deletes-by-name.
        //
        // Filesystem-only exports stay in McpServer: they never modify the project.

        #region plumbing (write)

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
                // One transaction per tool call: the edits commit together or not at all, and
                // the operator sees a single named entry in the TIA Portal undo stack instead of
                // an unlabelled pile of steps. Falls back to an unwrapped write when TIA Portal
                // refuses exclusive access, so this can never turn a working write into a
                // failure - see Portal.InTransaction in Portal.cs.
                return Portal.InTransaction($"MCP: {toolName}", body);
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

        private static string JoinPath(string groupPath, string name) =>
            string.IsNullOrEmpty(groupPath) ? name : $"{groupPath}/{name}";

        #endregion
    }
}
