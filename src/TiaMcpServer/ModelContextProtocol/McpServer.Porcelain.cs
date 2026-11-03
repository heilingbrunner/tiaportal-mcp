using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;
using SdkMcpServer = global::ModelContextProtocol.Server.McpServer;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // Porcelain tools: short, git-like commands for the chat ('tia pull', 'tia push preview')
        // on top of the plumbing tools, the way 'git pull' sits on top of fetch and merge.
        //
        //   tia init                  sets up '<exportPath>/tia.json' (args and export formats)
        //   tia connect <project>     OpenTiaProject
        //   tia pull                  ExportPlcProgram
        //   tia pull blocks [regex]   ExportBlocks
        //   tia pull types [regex]    ExportTypes
        //   tia pull tagtables [regex]     ExportTagTables
        //   tia pull block <path>     ExportBlock
        //   tia pull type <path>      ExportType
        //   tia pull tagtable <path>       ExportTagTable
        //   tia pull watchtables [regex]   ExportWatchTables
        //   tia pull watchtable <path>     ExportWatchTable
        //   tia pull forcetable <path>     ExportForceTable
        //   tia push                  ImportPlcProgramChanges with dryRun=false
        //   tia push preview          ImportPlcProgramChanges with dryRun=true
        //
        // 'pull' brings TIA Portal into the export folder, 'push' brings the changed files of that
        // folder into TIA Portal. The pull tools write the same tree as ExportPlcProgram, in the
        // formats of '<exportPath>/tia.json', so a pull of blocks and a pull of the whole program
        // land in the same folders. The push tools are the import tools with a different name,
        // they confirm and preview exactly the same way.
        //
        // Callers: the MCP host, through tool registration; the push tools only under
        // '--allow-write'. Affected API: none existing - the tools are new. File I/O: as
        // ExportPlcProgram and ImportPlcProgramChanges.

        #region porcelain

        [McpServerTool(Name = "TiaConnect", Title = "Tia connect", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia connect <project>': connect to TIA Portal (attach to the running instance, or start one) and open the project or local session, the first step before any 'tia pull' or 'tia push'. It is 'OpenTiaProject' under a shorter name; see there for the result: 'portalId' and the device and PLC software paths, whose PLC software path is the 'softwarePath' of the pull and push tools. Closes the project or session that is open in that instance first, unless it is the one asked for")]
        public static Task<ResponseOpenTiaProject> TiaConnect(
            [Description("path: full path of the .apXX project or .alsXX session file on the machine running this server")] string path,
            [Description("portalId: process id of the TIA Portal instance to open it in (see 'GetPortals'). Leave empty when at most one instance is running")] int? portalId = null,
            CancellationToken cancellationToken = default)
        {
            return OpenTiaProject(path, portalId, cancellationToken);
        }

        [McpServerTool(Name = "TiaInit", Title = "Tia init", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia init': set up '<exportPath>/tia.json', the file with the export formats of TIA Portal ('version-control-interface') and the 'args' (project, software-path, export-path, preserve-path) that other programs read. The server opens a dialog of the client in which the user checks or enters the four args and submits; do not answer it for the user. A missing file is created with the formats TIA Portal offers for a new workspace. An existing file is completed and never overwritten: missing keys are added, every existing value is kept. A client without dialogs uses the arguments or presets as they are. Tell the user to check the formats against Options > Settings > Version control interface > Export formats. Never opens or changes the TIA Portal project")]
        public static async Task<ResponseTiaInit> TiaInit(
            SdkMcpServer server,
            [Description("exportPath: directory on this machine that holds tia.json; the file is created there. The dialog starts with this value")] string exportPath = "",
            [Description("path: full path of the .apXX project or .alsXX session file; written to 'args' as 'project'. The dialog starts with this value; blank leaves it out")] string path = "",
            [Description("softwarePath: path of the PLC software in the project structure; written to 'args' as 'software-path'. The dialog starts with this value; blank leaves it out")] string softwarePath = "",
            [Description("preservePath: written to 'args' as 'preserve-path' when true. The dialog starts with this value; false leaves it out")] bool preservePath = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var args = new Dictionary<string, string>
                {
                    [ExportFormats.ArgsProjectKey] = path,
                    [ExportFormats.ArgsSoftwarePathKey] = softwarePath,
                    [ExportFormats.ArgsExportPathKey] = exportPath,
                    [ExportFormats.ArgsPreservePathKey] = preservePath ? "true" : string.Empty
                };

                if (server.ClientCapabilities?.Elicitation != null)
                {
                    var answered = await AskTiaInitAsync(server, args, cancellationToken).ConfigureAwait(false);

                    if (answered == null)
                    {
                        return new ResponseTiaInit
                        {
                            Message = "Nothing was written: the user did not submit the tia init dialog. Do not ask again unless the user asks for it.",
                            Meta = Ok(new JsonObject { ["changed"] = false })
                        };
                    }

                    args = answered;
                }

                var root = args[ExportFormats.ArgsExportPathKey];

                if (string.IsNullOrWhiteSpace(root))
                {
                    throw new McpException(
                        "The export path is empty, so tia.json has no place. Pass 'exportPath', enter it in the dialog, or start the server with '--export-path'.");
                }

                var result = ExportFormatSettings.Init(Path.GetFullPath(root), args);

                return new ResponseTiaInit
                {
                    Message = InitMessage(result),
                    File = result.File,
                    Created = result.Created,
                    Changed = result.Changed,
                    AddedArgs = result.AddedArgs,
                    KeptArgs = result.KeptArgs,
                    AddedFormats = result.AddedFormats,
                    Hint = result.AddedFormats.Count > 0
                        ? "Please check the export formats against Options > Settings > Version control interface > Export formats and correct the file if your settings differ."
                        : null,
                    Meta = Ok(new JsonObject
                    {
                        ["created"] = result.Created,
                        ["changed"] = result.Changed
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (System.Exception ex) when (ex is not McpException && ex is not System.OperationCanceledException)
            {
                throw new McpException($"Unexpected error setting up tia.json: {ex.Message}", ex);
            }
        }

        private static string InitMessage(TiaJsonInitResult result)
        {
            if (!result.Changed)
            {
                return $"'{result.File}' already has everything; nothing was changed.";
            }

            var kept = result.KeptArgs.Count > 0
                ? $" Kept the existing value of: {string.Join(", ", result.KeptArgs)}."
                : string.Empty;

            return (result.Created ? $"Created '{result.File}'." : $"Completed '{result.File}'.") +
                   $" Added {result.AddedArgs.Count} arg(s) and {result.AddedFormats.Count} export format(s).{kept}";
        }

        /// <summary>
        /// The dialog of 'tia init': the four args of tia.json, each starting with the value the
        /// call or preset gave. Null when the user declines or cancels.
        /// </summary>
        private static async Task<Dictionary<string, string>?> AskTiaInitAsync(
            SdkMcpServer server, IReadOnlyDictionary<string, string> start, CancellationToken cancellationToken)
        {
            ElicitRequestParams.PrimitiveSchemaDefinition Field(string key, string title, string description) =>
                new ElicitRequestParams.StringSchema
                {
                    Title = title,
                    Description = description,
                    Default = string.IsNullOrWhiteSpace(start[key]) ? null : start[key]
                };

            var result = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = "Set up tia.json: check the args other programs read from it. Missing keys are added, existing values are kept.",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        [ExportFormats.ArgsProjectKey] = Field(ExportFormats.ArgsProjectKey, "Project", "Full path of the .apXX project or .alsXX session file (optional)"),
                        [ExportFormats.ArgsSoftwarePathKey] = Field(ExportFormats.ArgsSoftwarePathKey, "Software path", "Path of the PLC software, e.g. PLC_1 (optional)"),
                        [ExportFormats.ArgsExportPathKey] = Field(ExportFormats.ArgsExportPathKey, "Export path", "Folder that holds tia.json"),
                        [ExportFormats.ArgsPreservePathKey] = new ElicitRequestParams.BooleanSchema
                        {
                            Title = "Preserve path",
                            Description = "Keep the project tree folders below the export path (optional; only 'true' is written)",
                            Default = start[ExportFormats.ArgsPreservePathKey] == "true"
                        }
                    },
                    Required = new List<string> { ExportFormats.ArgsExportPathKey }
                }
            }, cancellationToken).ConfigureAwait(false);

            if (!result.IsAccepted)
            {
                return null;
            }

            return start.Keys.ToDictionary(
                key => key,
                key => key == ExportFormats.ArgsPreservePathKey
                    ? (ElicitedFlag(result, key) ? "true" : string.Empty)
                    : ElicitedText(result, key));
        }

        private static bool ElicitedFlag(ElicitResult result, string field)
        {
            return result.Content != null
                && result.Content.TryGetValue(field, out var value)
                && value.ValueKind == JsonValueKind.True;
        }

        private static string ElicitedText(ElicitResult result, string field)
        {
            return result.Content != null
                && result.Content.TryGetValue(field, out var value)
                && value.ValueKind == JsonValueKind.String
                    // Explorer's "Copy as path" adds quotes, which Path.GetFullPath rejects
                    ? (value.GetString() ?? string.Empty).Trim().Trim('"').Trim()
                    : string.Empty;
        }

        [McpServerTool(Name = "TiaPull", Title = "Tia pull", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull': get the whole PLC program (every block and PLC data type) out of TIA Portal into the export folder, the way 'git pull' updates the working tree. It is 'ExportPlcProgram' under a shorter name; see there for the formats ('tia.json'), 'Skipped' and 'Failures'. To pull only some objects use 'TiaPullBlocks', 'TiaPullTypes' or 'TiaPullTagTables'. Existing files of the same name are overwritten; tell the user to review the result with 'git status' and 'git diff' in the export folder")]
        public static ResponseGeneratedSources TiaPull(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath)
        {
            return ExportPlcProgram(softwarePath, exportPath);
        }

        [McpServerTool(Name = "TiaPullBlocks", Title = "Tia pull blocks", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull blocks' or 'tia pull blocks <regex>': get only the program blocks out of TIA Portal into the export folder, in the formats of '<exportPath>/tia.json', into the same folders as 'TiaPull'. With a regular expression only blocks whose name matches are pulled, e.g. '.+HMI'. Without one all blocks are pulled, but no PLC data types. It is 'ExportBlocks' under a shorter name; see there for the result and notes")]
        public static ResponseGeneratedSources TiaPullBlocks(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, pulls only blocks whose name matches. Empty (default) means all blocks")] string regexName = "")
        {
            return ExportBlocks(softwarePath, exportPath, regexName);
        }

        [McpServerTool(Name = "TiaPullTypes", Title = "Tia pull types", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull types' or 'tia pull types <regex>': get only the PLC data types out of TIA Portal into the export folder, in the format of '<exportPath>/tia.json', into the same folders as 'TiaPull'. With a regular expression only types whose name matches are pulled, e.g. '.+HMI'. Without one all types are pulled, but no blocks. It is 'ExportTypes' under a shorter name; see there for the result and notes")]
        public static ResponseGeneratedSources TiaPullTypes(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, pulls only PLC data types whose name matches. Empty (default) means all types")] string regexName = "")
        {
            return ExportTypes(softwarePath, exportPath, regexName);
        }

        [McpServerTool(Name = "TiaPullTagTables", Title = "Tia pull tag tables", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull tagtables' or 'tia pull tagtables <regex>': get the PLC tag tables out of TIA Portal into the export folder as XML files, one per table, into '<exportPath>/<softwarePath>/PLC tags/<groups>/', next to the folders of 'TiaPull'. With a regular expression only tag tables whose name matches are pulled, e.g. '.+HMI'. Without one all tag tables are pulled. It is 'ExportTagTables' under a shorter name. Tag tables have a single format, so 'tia.json' is not used. 'tia push' does not read them back, and there is no tool that imports tag tables")]
        public static ResponseGeneratedSources TiaPullTagTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, pulls only tag tables whose name matches. Empty (default) means all tag tables")] string regexName = "")
        {
            return ExportTagTables(softwarePath, exportPath, regexName);
        }

        [McpServerTool(Name = "TiaPullBlock", Title = "Tia pull block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull block <path>': get exactly one program block out of TIA Portal into the export folder, in the format of '<exportPath>/tia.json'. It is 'ExportBlock' under a shorter name; see there for the result and notes. For several blocks by name use 'TiaPullBlocks' with a regular expression")]
        public static ResponseGeneratedSources TiaPullBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath)
        {
            return ExportBlock(softwarePath, exportPath, blockPath);
        }

        [McpServerTool(Name = "TiaPullType", Title = "Tia pull type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull type <path>': get exactly one PLC data type out of TIA Portal into the export folder, in the format of '<exportPath>/tia.json'. It is 'ExportType' under a shorter name; see there for the result and notes. For several types by name use 'TiaPullTypes' with a regular expression")]
        public static ResponseGeneratedSources TiaPullType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath,
            [Description("typePath: full path to the PLC data type in the project structure, e.g. 'Group/Name'")] string typePath)
        {
            return ExportType(softwarePath, exportPath, typePath);
        }

        [McpServerTool(Name = "TiaPullTagTable", Title = "Tia pull tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull tagtable <path>': get exactly one PLC tag table out of TIA Portal into the export folder as an XML file. It is 'ExportTagTable' under a shorter name; tag tables have a single format, so 'tia.json' is not used. For several tag tables by name use 'TiaPullTagTables' with a regular expression")]
        public static ResponseGeneratedSources TiaPullTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'")] string tagTablePath)
        {
            return ExportTagTable(softwarePath, exportPath, tagTablePath);
        }

        [McpServerTool(Name = "TiaPullWatchTables", Title = "Tia pull watch tables", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull watchtables' or 'tia pull watchtables <regex>': get the PLC watch tables out of TIA Portal into the export folder as XML files, one per table, into the watch and force tables folder below '<exportPath>/<softwarePath>/', mirroring the watch table groups. With a regular expression only watch tables whose name matches are pulled. Without one all watch tables are pulled. It is 'ExportWatchTables' under a shorter name. Watch tables have a single format, so 'tia.json' is not used. 'tia push' does not read them back")]
        public static ResponseGeneratedSources TiaPullWatchTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, pulls only watch tables whose name matches. Empty (default) means all watch tables")] string regexName = "")
        {
            return ExportWatchTables(softwarePath, exportPath, regexName);
        }

        [McpServerTool(Name = "TiaPullWatchTable", Title = "Tia pull watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull watchtable <path>': get exactly one PLC watch table out of TIA Portal into the export folder as an XML file. It is 'ExportWatchTable' under a shorter name; watch tables have a single format, so 'tia.json' is not used. For several watch tables by name use 'TiaPullWatchTables' with a regular expression")]
        public static ResponseGeneratedSources TiaPullWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'")] string watchTablePath)
        {
            return ExportWatchTable(softwarePath, exportPath, watchTablePath);
        }

        [McpServerTool(Name = "TiaPullForceTable", Title = "Tia pull force table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia pull forcetable <path>': get the PLC force table out of TIA Portal into the export folder as an XML file, next to the watch tables. It is 'ExportForceTable' under a shorter name; a PLC has one force table, use 'GetForceTables' for its name")]
        public static ResponseGeneratedSources TiaPullForceTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("forceTablePath: path of the force table as 'GetForceTables' lists it")] string forceTablePath)
        {
            return ExportForceTable(softwarePath, exportPath, forceTablePath);
        }

        [WriteTool]
        [McpServerTool(Name = "TiaPush",Title = "Tia push", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia push': put the files of the export folder that git reports as changed (modified, staged and new) back into TIA Portal, the way 'git push' sends the local commits on. It is 'ImportPlcProgramChanges' with dryRun=false, so blocks and PLC data types of the same name in the open project are REPLACED. Call it right away and do not ask in the chat first: the server shows the user a dialog of the client that lists every file and has a 'Yes, import' checkbox, and imports nothing unless the user ticks it and submits, so do not answer for the user. A client without such dialogs cannot run it; then use 'TiaPushPreview'. Never save on your own: after the push tell the user the change is only in memory, and call SaveProject (SaveSession in a local session) only after the user explicitly says 'save project'. The folder must lie in a git repository and git must be installed. Formats, rounds, 'Failures', 'Ignored' and 'Deleted' work as in 'ImportPlcProgramChanges'")]
        public static Task<ResponseImportedProgram> TiaPush(
            SdkMcpServer server,
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that holds the tree 'TiaPull' wrote and that is a git working tree; changes are looked up below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, pushes only changed files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only: keep the objects of a file that were generated successfully even when others in it fail. Default false, which rolls back a whole file on any error. XML files are never partial")] bool keepOnError = false,
            CancellationToken cancellationToken = default)
        {
            WritePolicy.EnsureEnabled(nameof(TiaPush));

            // ImportProgramAsync would say 'use dryRun=true', an argument this tool does not have.
            if (server.ClientCapabilities?.Elicitation == null)
            {
                throw new McpException(
                    $"'{nameof(TiaPush)}' is refused: this client cannot show the confirmation dialog in which the user ticks '{ImportConfirmation}' and submits (MCP elicitation). " +
                    $"Use '{nameof(TiaPushPreview)}' to preview, and run the push from a client that supports confirmation dialogs.");
            }

            return ImportProgramAsync(server, nameof(TiaPush), softwarePath, exportPath, keepOnError, dryRun: false,
                preview => Portal.ImportPlcProgramChanges(softwarePath, exportPath, regexName, keepOnError, preview),
                cancellationToken);
        }

        [WriteTool]
        [McpServerTool(Name = "TiaPushPreview", Title = "Tia push preview", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this when the user says 'tia push preview': show what 'TiaPush' would put into TIA Portal, without importing anything: 'Table' is a markdown table of the affected files with the columns Git, Type and File (files git reports as deleted have the status D and cannot be imported), 'Planned' has the details per file, 'Ignored' lists the files that are skipped. It is 'ImportPlcProgramChanges' with dryRun=true. Show the 'Table' to the user as a table, unchanged; call 'TiaPush' only when the user asks for it")]
        public static Task<ResponseImportedProgram> TiaPushPreview(
            SdkMcpServer server,
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that holds the tree 'TiaPull' wrote and that is a git working tree; changes are looked up below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, previews only changed files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only; only echoed in the response of a preview")] bool keepOnError = false,
            CancellationToken cancellationToken = default)
        {
            WritePolicy.EnsureEnabled(nameof(TiaPushPreview));

            return ImportProgramAsync(server, nameof(TiaPushPreview), softwarePath, exportPath, keepOnError, dryRun: true,
                preview => Portal.ImportPlcProgramChanges(softwarePath, exportPath, regexName, keepOnError, preview),
                cancellationToken);
        }

        #endregion
    }
}
