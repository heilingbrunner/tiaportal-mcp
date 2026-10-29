using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

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
        // tree below '<exportPath>/<softwarePath>/', the same layout ExportSources and
        // ExportXmlBlocks write with preservePath, so the narrower tools can work on the same folder.
        //
        // 'exportPath' and 'softwarePath' are the argument names the command line presets fill in
        // ('--export-path', '--software-path'), so these tools take them like every other export.

        #region plc program

        [McpServerTool(Name = "ExportPlcProgram", Title = "Export PLC program", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export a whole PLC program in one call: every block and PLC data type of a PLC software, each in the format that suits it. SCL and STL blocks, data blocks and PLC data types are written as source files (.scl, .awl, .db, .udt) and LAD blocks as XML (.xml), into '<exportPath>/<softwarePath>/Program blocks/...' and '<exportPath>/<softwarePath>/PLC data types/...', mirroring the project groups, one file per object. Blocks in other languages (FBD, GRAPH, ...), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run; export those with 'ExportXmlBlock' or 'ExportAsDocuments'. 'OverwritePlcProgram' reads the folder back, 'ImportPlcProgramChanges' only the files git reports as changed. For only sources use 'ExportSources', for only XML use 'ExportXmlBlocks'")]
        public static ResponseGeneratedSources ExportPlcProgram(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten; files are written below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, exports only objects whose name matches. Empty means all")] string regexName = "")
        {
            return GeneratedMany(
                () => Portal.ExportPlcProgram(softwarePath, exportPath, regexName),
                $"PLC program of '{softwarePath}'",
                exportPath);
        }

        [WriteTool]
        [McpServerTool(Name = "OverwritePlcProgram", Title = "Overwrite PLC program", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this ONLY when the user explicitly wants the WHOLE program of a PLC software in TIA Portal replaced by the files of a folder (first import into a project, restore, a folder without git); it is the counterpart to 'ExportPlcProgram'. For a request to import, preview or apply modified, changed or edited files use 'ImportPlcProgramChanges' instead. Blocks and PLC data types of the same name in the open project are REPLACED by the file content; everything in the folder is taken, changed or not. .udt files become PLC data types, .db, .awl and .scl files are compiled as sources, and .xml files below the 'Program blocks' folder are imported as blocks (LAD). Each file goes into the block or PLC data type group its folder path implies; a group that does not exist makes that file fail instead of being created. By default 'dryRun' is true: nothing is imported and 'Planned' lists every object with 'create' or 'overwrite'. Show that list to the user, ask 'Import these now? Reply: yes, import', and call again with dryRun=false only after the user explicitly confirms with 'yes, import'. Never save on your own: after a real import tell the user the change is only in memory, and call SaveProject (SaveSession in a local session) only after the user explicitly says 'save project'. Files are imported in rounds: an object that needs another one (a LAD block calling an SCL block, an instance data block) is retried until nothing more can be imported; 'Rounds' says how many passes were made and 'Failures' lists what never succeeded. Other files, and .xml files outside the 'Program blocks' folder, are listed in 'Ignored'. For only sources use 'ImportSources', for one XML file use 'ImportXmlBlock'")]
        public static ResponseImportedProgram OverwritePlcProgram(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("importPath: directory on this machine that holds the tree ExportPlcProgram wrote; read recursively from <importPath>/<softwarePath>/")] string importPath,
            [Description("regexName: optional regular expression, imports only files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only: keep the objects of a file that were generated successfully even when others in it fail. Default false, which rolls back a whole file on any error. XML files are never partial")] bool keepOnError = false,
            [Description("dryRun: default true, only lists what would be imported and changes nothing. Set false to really overwrite, and only after the user has seen the dry run list and answered 'yes, import'")] bool dryRun = true)
        {
            return Guarded(nameof(OverwritePlcProgram), () =>
                ImportedProgram(Portal.OverwritePlcProgram(softwarePath, importPath, regexName, keepOnError, dryRun), importPath, keepOnError));
        }

        [WriteTool]
        [McpServerTool(Name = "ImportPlcProgramChanges", Title = "Import PLC program changes", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this by default to import edited files back into TIA Portal, for any request to import, preview or apply modified or changed files: it takes only the files of an export folder that git reports as changed since the last commit (modified, staged and new files). For the whole folder use 'OverwritePlcProgram'. Blocks and PLC data types of the same name in the open project are REPLACED. Typical flow: 'ExportPlcProgram' into a git repository, commit the export as baseline, edit files, then use this. The folder must lie in a git repository and git must be installed. Files git reports as deleted cannot be imported and are listed in 'Deleted'; files with an open merge conflict are listed in 'Ignored'. By default 'dryRun' is true: nothing is imported and 'Planned' lists every object with 'create' or 'overwrite'. Show that list to the user, ask 'Import these now? Reply: yes, import', and call again with dryRun=false only after the user explicitly confirms with 'yes, import'. Never save on your own: after a real import tell the user the change is only in memory, and call SaveProject (SaveSession in a local session) only after the user explicitly says 'save project'. Formats, groups, rounds, 'Failures' and 'Ignored' work as in 'OverwritePlcProgram'")]
        public static ResponseImportedProgram ImportPlcProgramChanges(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("exportPath: directory on this machine that holds the tree ExportPlcProgram wrote and that is a git working tree; changes are looked up below <exportPath>/<softwarePath>/")] string exportPath,
            [Description("regexName: optional regular expression, imports only changed files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only: keep the objects of a file that were generated successfully even when others in it fail. Default false, which rolls back a whole file on any error. XML files are never partial")] bool keepOnError = false,
            [Description("dryRun: default true, only lists what would be imported and changes nothing. Set false to really overwrite, and only after the user has seen the dry run list and answered 'yes, import'")] bool dryRun = true)
        {
            return Guarded(nameof(ImportPlcProgramChanges), () =>
                ImportedProgram(Portal.ImportPlcProgramChanges(softwarePath, exportPath, regexName, keepOnError, dryRun), exportPath, keepOnError));
        }

        private static ResponseImportedProgram ImportedProgram(PlcProgramImportResult result, string path, bool keepOnError)
        {
            var overwrites = result.Planned.Count(p => p.Action == PlannedProgramItem.Overwrite);

            return new ResponseImportedProgram
            {
                Directory = result.Directory,
                DryRun = result.DryRun,
                Written = result.Written,
                Planned = result.Planned.Select(p => new ResponsePlannedProgramItem
                {
                    File = p.File,
                    Name = p.Name,
                    Path = p.Path,
                    Kind = p.Kind,
                    Format = p.Format,
                    Action = p.Action
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
                Message = result.DryRun
                    ? $"DRY RUN, nothing was imported: {result.Planned.Count} file(s) from '{path}' would be imported, {overwrites} of them overwrite existing objects, {result.Ignored.Count} ignored, {result.Deleted.Count} deleted. Show this list to the user and ask: 'Import these now? Reply: yes, import'. Call again with dryRun=false only after the user answers 'yes, import'."
                    : $"{result.Items.Count} object(s) imported from '{path}' in {result.Rounds} round(s), {result.Failures.Count} failed. The change is only in memory. Do not save on your own: ask the user, and call '{(Portal.IsLocalSession ? "SaveSession" : "SaveProject")}' only after the user explicitly says 'save project'.",
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
