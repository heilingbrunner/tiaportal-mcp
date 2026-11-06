using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Git;
using TiaMcpServer.Siemens;
using SdkMcpServer = global::ModelContextProtocol.Server.McpServer;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // The whole program of one PLC in one call: ExportPlcProgram writes it, OverwritePlcProgram
        // reads it back, ImportPlcProgramChanges reads back only the files git reports as
        // changed. Source files for SCL, STL, data blocks and PLC data types, XML for LAD. The
        // The import tools replace objects of the same name, so they preview (dryRun) by default.
        //
        // Callers: the MCP host, through tool registration; the import tools only under
        // '--allow-write'. Affected API: none existing - the tools are new. File I/O: a folder
        // tree below '<exportPath>/<softwarePath>/'. ExportBlocks, ExportTypes and ExportTagTables write
        // slices of the same tree, ExportBlock, ExportType and ExportTagTable single objects of it.
        //
        // 'exportPath' and 'softwarePath' are the argument names the command line presets fill in
        // ('--export-path', '--software-path'), so these tools take them like every other export.

        #region plc program

        [McpServerTool(Name = "ExportPlcProgram", Title = "Export PLC program", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export a whole PLC program in one call: every block and PLC data type of a PLC software, each in the format TIA Portal is configured for (Version control interface > Export formats): SIMATIC SD documents (.s7dcl, .s7res), XML (.xml) or a source file (.scl, .awl, .db, .udt). Openness cannot read that setting, so it is read from '<exportPath>/tia.json'. When that file does not exist it is created with the defaults TIA Portal offers for a new workspace and the response carries a note asking the user to check the formats: pass that note on to the user. An entry missing for an object type of the PLC fails the call before anything is written (see 'GetExportFormats'). SIMATIC SD documents need the object to be prepared in TIA Portal (select it, Tools > Prepare for Simatic SD), which Openness cannot do: an object that is not prepared is listed in 'Failures' with the message of TIA Portal, so tell the user to prepare it and export again. Files go into '<exportPath>/<softwarePath>/Program blocks/...' and '<exportPath>/<softwarePath>/PLC data types/...', mirroring the project groups, one file per object. Blocks of a language without a setting, blocks that cannot be written in the configured format, inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run; read those with 'GetXmlBlock'. 'OverwritePlcProgram' and 'ImportPlcProgramChanges' import sources, XML and SIMATIC SD documents (.s7dcl with .s7res). 'OverwritePlcProgram' reads the folder back, 'ImportPlcProgramChanges' only the files git reports as changed. For only some objects use 'ExportBlocks', 'ExportTypes' or 'ExportTagTables' (with a regular expression), or 'ExportBlock', 'ExportType' or 'ExportTagTable' for exactly one")]
        public static ResponseGeneratedSources ExportPlcProgram(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath)
        {
            return GeneratedMany(
                () => Portal.ExportPlcProgram(softwarePath, exportPath),
                $"PLC program of '{softwarePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportBlocks", Title = "Export blocks", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export only the program blocks of a PLC software, each in the format TIA Portal is configured for its block type and language (Version control interface > Export formats): SIMATIC SD documents (.s7dcl, .s7res), XML (.xml) or a source file (.scl, .awl, .db). Openness cannot read that setting, so it is read from '<exportPath>/tia.json'; a missing file is created with the defaults of TIA Portal for a new workspace and the response carries a note asking the user to check the formats, which you pass on (see 'GetExportFormats'). An entry missing for a block type of the PLC fails the call before anything is written. With 'regexName' only blocks whose name matches are exported, e.g. '.+HMI'; without it all blocks. No PLC data types and no tag tables are written. Files go into '<exportPath>/<softwarePath>/Program blocks/...', mirroring the project groups, one file per block, the same folders as 'ExportPlcProgram'. Blocks of a language without a setting, blocks that cannot be written in the configured format, inconsistent and know-how protected ones are reported in 'Skipped'; SIMATIC SD documents need the block to be prepared in TIA Portal, otherwise it is listed in 'Failures'. Use 'ExportTypes' for the PLC data types, 'ExportTagTables' for the tag tables, 'ExportPlcProgram' for blocks and types in one call")]
        public static ResponseGeneratedSources ExportBlocks(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath,
            [Description("regexName: optional regular expression, exports only blocks whose name matches. Empty (default) means all blocks")] string regexName = "")
        {
            return GeneratedMany(
                () => Portal.ExportPlcProgram(softwarePath, exportPath, blocks: true, types: false, regexName),
                $"Blocks of '{softwarePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportTypes", Title = "Export PLC data types", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export only the PLC data types of a PLC software, in the format TIA Portal is configured for them (Version control interface > Export formats, entry 'PLC data types'): SIMATIC SD documents (.s7dcl, .s7res), XML (.xml) or a source file (.udt). Openness cannot read that setting, so it is read from '<exportPath>/tia.json'; a missing file is created with the defaults of TIA Portal for a new workspace and the response carries a note asking the user to check the formats, which you pass on (see 'GetExportFormats'). A missing 'PLC data types' entry fails the call before anything is written. With 'regexName' only types whose name matches are exported, e.g. '.+HMI'; without it all types. No blocks and no tag tables are written. Files go into '<exportPath>/<softwarePath>/PLC data types/...', mirroring the project groups, one file per type, the same folders as 'ExportPlcProgram'. Inconsistent types are reported in 'Skipped'; SIMATIC SD documents need the type to be prepared in TIA Portal, otherwise it is listed in 'Failures'. Use 'ExportBlocks' for the program blocks, 'ExportTagTables' for the tag tables, 'ExportPlcProgram' for blocks and types in one call")]
        public static ResponseGeneratedSources ExportTypes(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath,
            [Description("regexName: optional regular expression, exports only PLC data types whose name matches. Empty (default) means all types")] string regexName = "")
        {
            return GeneratedMany(
                () => Portal.ExportPlcProgram(softwarePath, exportPath, blocks: false, types: true, regexName),
                $"PLC data types of '{softwarePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportTagTables", Title = "Export tag tables", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export the PLC tag tables of a PLC software as XML files, one per table, into '<exportPath>/<softwarePath>/PLC tags/<groups>/', next to the folders of 'ExportPlcProgram', 'ExportBlocks' and 'ExportTypes'. Unlike blocks and PLC data types, tag tables have no entry in the export format settings of TIA Portal (Version control interface > Export formats) and therefore none in '<exportPath>/tia.json': they are always written as XML, and the file is neither read nor created. With 'regexName' only tag tables whose name matches are exported, e.g. '.+HMI'; without it all tag tables. Tag tables that cannot be written are listed in 'Failures'. 'OverwritePlcProgram' and 'ImportPlcProgramChanges' do not read them back, and there is no tool that imports tag tables")]
        public static ResponseGeneratedSources ExportTagTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, exports only tag tables whose name matches. Empty (default) means all tag tables")] string regexName = "")
        {
            return GeneratedMany(
                () => Portal.ExportPlcTagTables(softwarePath, exportPath, regexName),
                $"Tag tables of '{softwarePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportBlock", Title = "Export block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export exactly one program block, in the format TIA Portal is configured for its block type and language, read from '<exportPath>/tia.json' (SIMATIC SD documents, XML or a source file; see 'ExportBlocks' and 'GetExportFormats' for the rules, the note about a created tia.json and 'Skipped' and 'Failures'). The file lands in the same folder and under the same name as with 'ExportBlocks' and 'ExportPlcProgram': '<exportPath>/<softwarePath>/Program blocks/<groups>/'. An existing file is overwritten. For several blocks by name use 'ExportBlocks' with a regular expression")]
        public static ResponseGeneratedSources ExportBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath)
        {
            return GeneratedMany(
                () => Portal.ExportPlcBlock(softwarePath, exportPath, blockPath),
                $"Block '{blockPath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportType", Title = "Export PLC data type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export exactly one PLC data type, in the format TIA Portal is configured for PLC data types, read from the entry 'PLC data types' of '<exportPath>/tia.json' (see 'ExportTypes' and 'GetExportFormats' for the rules, the note about a created tia.json and 'Skipped' and 'Failures'). The file lands in the same folder and under the same name as with 'ExportTypes' and 'ExportPlcProgram': '<exportPath>/<softwarePath>/PLC data types/<groups>/'. An existing file is overwritten. For several types by name use 'ExportTypes' with a regular expression")]
        public static ResponseGeneratedSources ExportType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/; <exportPath>/tia.json is read from here and created with defaults when missing")] string exportPath,
            [Description("typePath: full path to the PLC data type in the project structure, e.g. 'Group/Name'")] string typePath)
        {
            return GeneratedMany(
                () => Portal.ExportPlcType(softwarePath, exportPath, typePath),
                $"PLC data type '{typePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportTagTable", Title = "Export tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export exactly one PLC tag table as an XML file into '<exportPath>/<softwarePath>/PLC tags/<groups>/', next to the folders of 'ExportBlock' and 'ExportType'. Tag tables have no entry in the export format settings of TIA Portal, so '<exportPath>/tia.json' is not read: they are always XML. An existing file is overwritten. For several tag tables by name use 'ExportTagTables' with a regular expression")]
        public static ResponseGeneratedSources ExportTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'")] string tagTablePath)
        {
            return GeneratedMany(
                () => Portal.ExportPlcTagTable(softwarePath, exportPath, tagTablePath),
                $"Tag table '{tagTablePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportWatchTables", Title = "Export watch tables", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export the PLC watch tables of a PLC software as XML files, one per table, into '<exportPath>/<softwarePath>/<watch and force tables folder>/<groups>/' (the folder is named as TIA Portal names it in the current interface language), next to the folders of 'ExportPlcProgram'. Watch tables have no entry in the export format settings of TIA Portal, so '<exportPath>/tia.json' is not read: they are always XML. With 'regexName' only watch tables whose name matches are exported; without it all watch tables. Tables that cannot be written are listed in 'Failures'. The force table is not included, use 'ExportForceTable'. 'OverwritePlcProgram' and 'ImportPlcProgramChanges' do not read watch tables back; 'ImportWatchTable' imports one")]
        public static ResponseGeneratedSources ExportWatchTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, exports only watch tables whose name matches. Empty (default) means all watch tables")] string regexName = "")
        {
            return GeneratedMany(
                () => Portal.ExportPlcWatchTables(softwarePath, exportPath, regexName),
                $"Watch tables of '{softwarePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportWatchTable", Title = "Export watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export exactly one PLC watch table as an XML file into '<exportPath>/<softwarePath>/<watch and force tables folder>/<groups>/', the same file as with 'ExportWatchTables'. Watch tables are always XML, '<exportPath>/tia.json' is not read. An existing file is overwritten. For several watch tables by name use 'ExportWatchTables' with a regular expression")]
        public static ResponseGeneratedSources ExportWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'")] string watchTablePath)
        {
            return GeneratedMany(
                () => Portal.ExportPlcWatchTable(softwarePath, exportPath, watchTablePath),
                $"Watch table '{watchTablePath}'",
                exportPath);
        }

        [McpServerTool(Name = "ExportForceTable", Title = "Export force table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export the PLC force table as an XML file into the watch and force tables folder below '<exportPath>/<softwarePath>/', next to the watch tables of 'ExportWatchTables'. A PLC has one force table, owned by the system; use 'GetForceTables' for its name. Always XML, '<exportPath>/tia.json' is not read. An existing file is overwritten")]
        public static ResponseGeneratedSources ExportForceTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("forceTablePath: path of the force table as 'GetForceTables' lists it")] string forceTablePath)
        {
            return GeneratedMany(
                () => Portal.ExportPlcForceTable(softwarePath, exportPath, forceTablePath),
                $"Force table '{forceTablePath}'",
                exportPath);
        }

        [WriteTool]
        [McpServerTool(Name = "OverwritePlcProgram", Title = "Overwrite PLC program", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this ONLY when the user explicitly wants the WHOLE program of a PLC software in TIA Portal replaced by the files of a folder (first import into a project, restore, a folder without git); it is the counterpart to 'ExportPlcProgram'. For a request to import, preview or apply modified, changed or edited files use 'ImportPlcProgramChanges' instead. Blocks and PLC data types of the same name in the open project are REPLACED by the file content; everything in the folder is taken, changed or not. .udt files become PLC data types, .db, .awl and .scl files are compiled as sources, and .xml files below the 'Program blocks' folder are imported as blocks (LAD), and .s7dcl files (SIMATIC SD documents, with their .s7res) below the 'Program blocks' or 'PLC data types' folder as blocks (TIA Portal V20 or newer) or PLC data types (V21 or newer). Each file goes into the block or PLC data type group its folder path implies; a group that does not exist makes that file fail instead of being created. By default 'dryRun' is true: nothing is imported and 'Planned' lists every object with 'create' or 'overwrite'. Show that list to the user and call again with dryRun=false only after the user agrees. The server then asks the user itself to confirm by ticking the 'Yes, import' checkbox in a dialog of the client and imports nothing without it, so do not answer for the user; a client without such dialogs cannot run a real import. Never save on your own: after a real import tell the user the change is only in memory, and call SaveProject (SaveSession in a local session) only after the user explicitly says 'save project'. Files are imported in rounds: an object that needs another one (a LAD block calling an SCL block, an instance data block) is retried until nothing more can be imported; 'Rounds' says how many passes were made and 'Failures' lists what never succeeded. Other files, and .xml files outside the 'Program blocks' folder, are listed in 'Ignored'. To import only the files that changed since the last git commit use 'ImportPlcProgramChanges'")]
        public static Task<ResponseImportedProgram> OverwritePlcProgram(
            SdkMcpServer server,
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("importPath: directory on this machine that holds the tree ExportPlcProgram wrote; read recursively from <importPath>/<softwarePath>/")] string importPath,
            [Description("regexName: optional regular expression, imports only files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only: keep the objects of a file that were generated successfully even when others in it fail. Default false, which rolls back a whole file on any error. XML files are never partial")] bool keepOnError = false,
            [Description("dryRun: default true, only lists what would be imported and changes nothing. Set false to really overwrite, and only after the user has seen the dry run list and agreed; the server then asks the user to tick 'Yes, import' in a dialog")] bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            return ImportProgramAsync(server, nameof(OverwritePlcProgram), softwarePath, importPath, keepOnError, dryRun,
                preview => Portal.OverwritePlcProgram(softwarePath, importPath, regexName, keepOnError, preview),
                cancellationToken);
        }

        [WriteTool]
        [McpServerTool(Name = "ImportPlcProgramChanges", Title = "Import PLC program changes", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this by default to import edited files back into TIA Portal, for any request to import, preview or apply modified or changed files: it takes only the files of an export folder that git reports as changed since the last commit (modified, staged and new files). For the whole folder use 'OverwritePlcProgram'. Blocks and PLC data types of the same name in the open project are REPLACED. Typical flow: 'ExportPlcProgram' into a git repository, commit the export as baseline, edit files, then use this. The folder must lie in a git repository and git must be installed. Files git reports as deleted cannot be imported and are listed in 'Deleted'; files with an open merge conflict are listed in 'Ignored'. To import, call it with dryRun=false right away and do not ask in the chat first: the server then shows the user a dialog of the client that lists every file to import and has a 'Yes, import' checkbox, and imports nothing unless the user ticks it and submits, so do not answer for the user. Use dryRun=true (the default) only when the user wants just the list: nothing is imported and 'Planned' lists every object with 'create' or 'overwrite'. A client without such dialogs cannot run a real import; then use dryRun=true. Never save on your own: after a real import tell the user the change is only in memory, and call SaveProject (SaveSession in a local session) only after the user explicitly says 'save project'. Formats, groups, rounds, 'Failures' and 'Ignored' work as in 'OverwritePlcProgram'")]
        public static Task<ResponseImportedProgram> ImportPlcProgramChanges(
            SdkMcpServer server,
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that holds the tree ExportPlcProgram wrote and that is a git working tree; changes are looked up below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, imports only changed files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only: keep the objects of a file that were generated successfully even when others in it fail. Default false, which rolls back a whole file on any error. XML files are never partial")] bool keepOnError = false,
            [Description("dryRun: default true, only lists what would be imported and changes nothing. Set false to import: the server then shows the user a dialog with every file to import and a 'Yes, import' checkbox, so do not ask in the chat first")] bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            return ImportProgramAsync(server, nameof(ImportPlcProgramChanges), softwarePath, exportPath, keepOnError, dryRun,
                preview => Portal.ImportPlcProgramChanges(softwarePath, exportPath, regexName, keepOnError, preview),
                cancellationToken);
        }

        /// <summary>
        /// The label of the checkbox the user ticks in the confirmation dialog before a real import.
        /// The agent cannot tick it: the server asks the user through the client.
        /// </summary>
        public const string ImportConfirmation = "Yes, import";

        private const string ConfirmationField = "confirmation";

        /// <summary>
        /// The one option of the confirmation field. A multi-select with a single option is what
        /// clients draw as one checkbox; a boolean field comes out as a False/True picker in some
        /// of them. The option has no separate title, so the label is its value.
        /// </summary>
        private const string ConfirmationValue = ImportConfirmation;

        /// <summary>True only when the user submitted the dialog with the confirmation checkbox ticked.</summary>
        public static bool IsImportAccepted(ElicitResult result)
        {
            return result.IsAccepted
                && result.Content != null
                && result.Content.TryGetValue(ConfirmationField, out var answer)
                && answer.ValueKind == JsonValueKind.Array
                && answer.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.String && v.GetString() == ConfirmationValue);
        }

        /// <summary>
        /// A dry run is a plain call. A real import first shows the user what it would do, in a
        /// confirmation dialog of the client (MCP elicitation), and runs only when the user ticks
        /// <see cref="ImportConfirmation"/> and submits. The dialog opens before the exclusive-access
        /// transaction starts, so TIA Portal is not held while the user thinks. A client without
        /// elicitation is refused: without it nothing but the agent's own restraint would stand
        /// between a prompt and a replaced program.
        /// </summary>
        private static async Task<ResponseImportedProgram> ImportProgramAsync(
            SdkMcpServer server, string toolName, string softwarePath, string path, bool keepOnError, bool dryRun,
            Func<bool, PlcProgramImportResult> run, CancellationToken cancellationToken)
        {
            if (dryRun)
            {
                return Guarded(toolName, () => ImportedProgram(run(true), path, keepOnError));
            }

            if (server.ClientCapabilities?.Elicitation == null)
            {
                throw new McpException(
                    $"'{toolName}' with dryRun=false is refused: this client cannot show the confirmation dialog in which the user ticks '{ImportConfirmation}' and submits (MCP elicitation). " +
                    "Use dryRun=true to preview, and run the import from a client that supports confirmation dialogs.");
            }

            var preview = Guarded(toolName, () => run(true));

            if (preview.Planned.Count == 0)
            {
                return ImportedProgram(preview, path, keepOnError);
            }

            if (!await ConfirmImportAsync(server, softwarePath, preview, cancellationToken).ConfigureAwait(false))
            {
                var declined = ImportedProgram(preview, path, keepOnError);

                declined.Message = $"Nothing was imported: the user did not tick '{ImportConfirmation}' and submit. Do not ask again unless the user asks for the import.";

                return declined;
            }

            return Guarded(toolName, () => ImportedProgram(run(false), path, keepOnError));
        }

        /// <summary>
        /// The letter in front of a file in the dialog: what git says (M, A, U, R). Without git, as
        /// with OverwritePlcProgram, an object that exists is shown as modified and a new one as added.
        /// </summary>
        public static string StatusLetter(PlannedProgramItem item)
        {
            if (item.GitStatus != null)
            {
                return item.GitStatus;
            }

            return item.Action == PlannedProgramItem.Overwrite ? "M" : item.Action == PlannedProgramItem.Create ? "A" : "?";
        }

        /// <summary>
        /// The files a dry run affects as a markdown table with the columns Git, Type and File: every
        /// planned file with its git status and kind (block or type), then the files git reports as
        /// deleted, which cannot be imported. Null when there is nothing to list.
        /// </summary>
        public static string? PlannedTable(PlcProgramImportResult result)
        {
            var rows = result.Planned
                .Select(p => new[] { StatusLetter(p), p.Kind, p.File })
                .Concat(result.Deleted.Select(f => new[] { GitChangeKind.Deleted.Letter(), null, f }))
                .ToList();

            if (rows.Count == 0)
            {
                return null;
            }

            var lines = new List<string>
            {
                "| Git | Type | File |",
                "| --- | --- | --- |"
            };

            lines.AddRange(rows.Select(r => "| " + string.Join(" | ", r.Select(c => (c ?? string.Empty).Replace("|", "\\|"))) + " |"));

            return string.Join(Environment.NewLine, lines);
        }

        private static async Task<bool> ConfirmImportAsync(
            SdkMcpServer server, string softwarePath, PlcProgramImportResult preview, CancellationToken cancellationToken)
        {
            var overwrites = preview.Planned.Count(p => p.Action == PlannedProgramItem.Overwrite);
            var listed = preview.Planned
                .Select(p => $"- {StatusLetter(p)}: {p.File}")
                .Concat(preview.Deleted.Select(f => $"- {GitChangeKind.Deleted.Letter()}: {f} (cannot be imported)"));

            var notImported = preview.Ignored.Count > 0
                ? $"{Environment.NewLine}Not imported: {preview.Ignored.Count} file(s) are ignored."
                : string.Empty;

            var result = await server.ElicitAsync(new ElicitRequestParams
            {
                Message =
                    $"Import {preview.Planned.Count} file(s) into '{softwarePath}' of the open TIA Portal project? " +
                    $"{overwrites} existing object(s) will be overwritten.{Environment.NewLine}" +
                    string.Join(Environment.NewLine, listed) + notImported,
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        [ConfirmationField] = new ElicitRequestParams.UntitledMultiSelectEnumSchema
                        {
                            Title = "Confirmation",
                            MinItems = 1,
                            Items = new ElicitRequestParams.UntitledEnumItemsSchema
                            {
                                Enum = new List<string> { ConfirmationValue }
                            }
                        }
                    },
                    Required = new List<string> { ConfirmationField }
                }
            }, cancellationToken).ConfigureAwait(false);

            return IsImportAccepted(result);
        }

        private static ResponseImportedProgram ImportedProgram(PlcProgramImportResult result, string path, bool keepOnError)
        {
            var overwrites = result.Planned.Count(p => p.Action == PlannedProgramItem.Overwrite);

            return new ResponseImportedProgram
            {
                Directory = result.Directory,
                DryRun = result.DryRun,
                Table = result.DryRun ? PlannedTable(result) : null,
                Written = result.Written,
                Planned = result.Planned.Select(p => new ResponsePlannedProgramItem
                {
                    File = p.File,
                    Name = p.Name,
                    Path = p.Path,
                    Kind = p.Kind,
                    Format = p.Format,
                    Action = p.Action,
                    GitStatus = p.GitStatus
                }).ToList(),
                Items = result.Items.Select(i => new ResponseImportedProgramItem
                {
                    Name = i.Name,
                    Path = i.Path,
                    Kind = i.Kind,
                    Format = i.Format
                }).ToList(),
                Failures = result.Failures,
                Ignored = result.Ignored,
                Deleted = result.Deleted,
                Changed = result.Changed,
                Rounds = result.Rounds,
                Report = ReportFormatter.Compose(
                    ReportFormatter.Imported(result.Items.Count, result.Planned.Count, result.Rounds, result.Failures, result.Ignored, result.Deleted),
                    result.DryRun ? PlannedTable(result) : null),
                Message = result.DryRun
                    ? $"DRY RUN, nothing was imported: {result.Planned.Count} file(s) from '{path}' would be imported, {overwrites} of them overwrite existing objects, {result.Ignored.Count} ignored, {result.Deleted.Count} deleted. Show the 'Report' to the user as tables, unchanged. Call again with dryRun=false only after the user agrees; the server then asks the user to confirm by ticking the '{ImportConfirmation}' checkbox in a dialog of the client."
                    : $"{result.Items.Count} object(s) imported from '{path}' in {result.Rounds} round(s), {result.Failures.Count} failed. Show the 'Report' to the user as tables, unchanged. The change is only in memory. Do not save on your own: ask the user, and call '{(Portal.IsLocalSession ? "SaveSession" : "SaveProject")}' only after the user explicitly says 'save project'.",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["pendingSave"] = !result.DryRun,
                    ["dryRun"] = result.DryRun,
                    ["plannedCount"] = result.Planned.Count,
                    ["importedCount"] = result.Items.Count,
                    ["failedCount"] = result.Failures.Count,
                    ["rounds"] = result.Rounds,
                    ["keepOnError"] = keepOnError
                }
            };
        }

        #endregion
    }
}
