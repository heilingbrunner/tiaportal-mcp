using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
        #region PLC Program Templates

        [McpServerPrompt(Name = "ExportPlcProgram"), Description("Export all blocks and PLC data types: sources, and XML for LAD blocks")]
        public static string ExportPlcProgram(string softwarePath, string exportPath)
        {
            return $@"Export the whole program of one PLC software in one call, each object in the format that suits it.

What you get:
- .scl for SCL blocks, .awl for STL blocks, .db for data blocks and .udt for PLC data types, as external source files
- .xml for LAD blocks, because TIA Portal generates no source for them
- Blocks in other languages (FBD, GRAPH, ...), inconsistent and know-how protected objects are listed in 'Skipped'; use ExportXmlBlock or ExportAsDocuments for those
- Files are written below <exportPath>/<softwarePath>/Program blocks/... and <exportPath>/<softwarePath>/PLC data types/..., mirroring the project groups. OverwritePlcProgram reads the folder back, ImportPlcProgramChanges only the files git reports as changed

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten

Use the ExportPlcProgram tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}";
        }

        [McpServerPrompt(Name = "OverwritePlcProgram"), Description("Overwrite a PLC program with a folder written by ExportPlcProgram")]
        public static string OverwritePlcProgram(string softwarePath, string importPath, string regexName = "", string keepOnError = "false", string dryRun = "true")
        {
            return $@"Use this only when the user explicitly wants the whole program of one PLC software replaced by the files of a folder (first import, restore, a folder without git). For a request to import, preview or apply modified, changed or edited files use ImportPlcProgramChanges instead.

Overwrite the whole program of one PLC software in one call with the files of a folder, the counterpart to ExportPlcProgram (requires the server started with '--allow-write').

Blocks and PLC data types of the same name in the open project are REPLACED by the file content. Always preview first:
1. Call the tool with dryRun true. Nothing is imported; 'Planned' lists every object with 'create' or 'overwrite'
2. Show the list to the user, name the objects that would be overwritten, and ask whether to import
3. Only after the user agrees, call the tool again with dryRun false. The server then opens a confirmation dialog in which the user has to type 'yes, import'; do not answer it for the user. Without that answer nothing is imported, and a client without such dialogs cannot run the import
4. Do not save on your own. Tell the user the change is only in memory and ask whether to save; call SaveProject (or SaveSession in a local session) only after the user explicitly says 'save project'

What happens:
- .udt files become PLC data types; .db, .awl and .scl files are compiled as sources; .xml files below the 'Program blocks' folder are imported as blocks (LAD)
- Each file goes into the block or PLC data type group its folder path implies; the group must already exist (create it with CreateBlockGroup or CreateTypeGroup), otherwise that file fails
- Files are imported in rounds: an object that needs another one (a LAD block calling an SCL block, an instance data block) is retried until nothing more can be imported; the response shows 'Rounds' and lists what never succeeded in 'Failures'
- Other files, and .xml files outside the 'Program blocks' folder, are listed in 'Ignored'
- To take only the files changed since the last git commit use ImportPlcProgramChanges

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- importPath: folder that holds the tree ExportPlcProgram wrote; read from <importPath>/<softwarePath>/
- regexName: empty for all, or e.g. 'FB_.*' to select files by base name
- keepOnError: Use false (default) to roll back a whole source file on any error, true to keep the objects of a file that were generated successfully
- dryRun: true (default) only previews, false really overwrites

Use the OverwritePlcProgram tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- regexName: {regexName}
- keepOnError: {NormalizeBool(keepOnError)}
- dryRun: {NormalizeBool(dryRun)}";
        }

        [McpServerPrompt(Name = "ImportPlcProgramChanges"), Description("Overwrite only the files git reports as changed in an export folder")]
        public static string ImportPlcProgramChanges(string softwarePath, string exportPath, string regexName = "", string keepOnError = "false", string dryRun = "true")
        {
            return $@"This is the default way to import edited files back; for the whole folder use OverwritePlcProgram.

Overwrite objects of one PLC software with only the files of an export folder that git reports as changed since the last commit: modified, staged and new files (requires the server started with '--allow-write', git on PATH and an export folder inside a git repository).

Blocks and PLC data types of the same name in the open project are REPLACED by the file content. Always preview first:
1. Call the tool with dryRun true. Nothing is imported; 'Planned' lists every changed file with its object and 'create' or 'overwrite'
2. Show the list to the user, name the objects that would be overwritten, and ask whether to import
3. Only after the user agrees, call the tool again with dryRun false. The server then opens a confirmation dialog in which the user has to type 'yes, import'; do not answer it for the user. Without that answer nothing is imported, and a client without such dialogs cannot run the import
4. Do not save on your own. Tell the user the change is only in memory and ask whether to save; call SaveProject (or SaveSession in a local session) only after the user explicitly says 'save project'

Typical flow: ExportPlcProgram into the git folder, commit the export as baseline, edit files, then this tool.

What happens:
- Files git reports as deleted cannot be imported and are listed in 'Deleted'; files with an open merge conflict are listed in 'Ignored'
- Formats, groups, rounds, 'Failures' and 'Ignored' work as in OverwritePlcProgram

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: folder that holds the tree ExportPlcProgram wrote and is a git working tree; changes are looked up below <exportPath>/<softwarePath>/
- regexName: empty for all, or e.g. 'FB_.*' to select changed files by base name
- keepOnError: Use false (default) to roll back a whole source file on any error, true to keep the objects of a file that were generated successfully
- dryRun: true (default) only previews, false really overwrites

Use the ImportPlcProgramChanges tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- keepOnError: {NormalizeBool(keepOnError)}
- dryRun: {NormalizeBool(dryRun)}";
        }

        #endregion
    }
}
