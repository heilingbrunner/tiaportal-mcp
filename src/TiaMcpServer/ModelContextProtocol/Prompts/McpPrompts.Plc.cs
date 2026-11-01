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

What you get: every object in the format TIA Portal is configured for (Version control interface > Export formats):
- SIMATIC SD documents (.s7dcl and .s7res), SimaticML (.xml) or an external source file (.scl, .awl, .db, .udt), per object type
- The formats are read from <exportPath>/tia.json because Openness cannot read them. A missing file is created with the defaults TIA Portal offers for a new workspace; the response then has a note asking the user to check the file, so tell the user. An entry missing for an object type fails the export before anything is written. Call GetExportFormats to see the formats
- Blocks of a language without a setting, blocks that cannot be written in the configured format, inconsistent and know-how protected objects are listed in 'Skipped'; read those with GetXmlBlock
- Files are written below <exportPath>/<softwarePath>/Program blocks/... and <exportPath>/<softwarePath>/PLC data types/..., mirroring the project groups. OverwritePlcProgram reads the folder back, ImportPlcProgramChanges only the files git reports as changed

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten

Use the ExportPlcProgram tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}";
        }

        [McpServerPrompt(Name = "ExportBlocks"), Description("Export program blocks, all or by regular expression, using tia.json formats")]
        public static string ExportBlocks(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Export only the program blocks of one PLC software, each in the format TIA Portal is configured for its block type and language (Version control interface > Export formats).

What you get:
- The formats are read from <exportPath>/tia.json because Openness cannot read them. A missing file is created with the defaults TIA Portal offers for a new workspace; the response then has a note asking the user to check the file, so tell the user. An entry missing for a block type fails the export before anything is written. Call GetExportFormats to see the formats
- No PLC data types and no tag tables; use ExportTypes and ExportTagTables for those, or ExportPlcProgram for blocks and types in one call
- Blocks that cannot be written in the configured format, inconsistent and know-how protected blocks are listed in 'Skipped'
- Files are written below <exportPath>/<softwarePath>/Program blocks/..., mirroring the project groups

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten
- regexName: empty for all blocks, or e.g. '.+HMI' to export only blocks whose name matches

Use the ExportBlocks tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "ExportTypes"), Description("Export PLC data types, all or by regular expression, using tia.json formats")]
        public static string ExportTypes(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Export only the PLC data types of one PLC software, in the format TIA Portal is configured for them (Version control interface > Export formats, entry 'PLC data types').

What you get:
- The format is read from <exportPath>/tia.json because Openness cannot read it. A missing file is created with the defaults TIA Portal offers for a new workspace; the response then has a note asking the user to check the file, so tell the user. A missing 'PLC data types' entry fails the export before anything is written
- No blocks and no tag tables; use ExportBlocks and ExportTagTables for those, or ExportPlcProgram for blocks and types in one call
- Files are written below <exportPath>/<softwarePath>/PLC data types/..., mirroring the project groups

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten
- regexName: empty for all types, or e.g. '.+HMI' to export only types whose name matches

Use the ExportTypes tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "ExportTagTables"), Description("Export PLC tag tables as XML, all or by regular expression")]
        public static string ExportTagTables(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Export the PLC tag tables of one PLC software as XML files, one per table.

What you get:
- Tag tables have no entry in the export format settings of TIA Portal, so <exportPath>/tia.json is neither read nor created: they are always XML
- Files are written below <exportPath>/<softwarePath>/PLC tags/<groups>/, next to the folders of ExportBlocks and ExportTypes
- OverwritePlcProgram and ImportPlcProgramChanges do not read tag tables back, and there is no tool that imports tag tables

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten
- regexName: empty for all tag tables, or e.g. '.+HMI' to export only tables whose name matches

Use the ExportTagTables tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "ExportBlock"), Description("Export one program block in the format configured in tia.json")]
        public static string ExportBlock(string softwarePath, string exportPath, string blockPath)
        {
            return $@"Export exactly one program block, in the format TIA Portal is configured for its block type and language (Version control interface > Export formats). The format is read from <exportPath>/tia.json; a missing file is created with the defaults and the response then has a note asking the user to check it, so tell the user. The file lands in <exportPath>/<softwarePath>/Program blocks/<groups>/, where ExportBlocks and ExportPlcProgram put it. For several blocks by name use ExportBlocks.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default; an existing file of the same name is overwritten
- blockPath: full path of the block in the project structure, e.g. 'Group/Subgroup/Name'; call GetBlocks to find it

Use the ExportBlock tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "ExportType"), Description("Export one PLC data type in the format configured in tia.json")]
        public static string ExportType(string softwarePath, string exportPath, string typePath)
        {
            return $@"Export exactly one PLC data type, in the format TIA Portal is configured for PLC data types (Version control interface > Export formats). The format is read from the entry 'PLC data types' of <exportPath>/tia.json; a missing file is created with the defaults and the response then has a note asking the user to check it, so tell the user. The file lands in <exportPath>/<softwarePath>/PLC data types/<groups>/, where ExportTypes and ExportPlcProgram put it. For several types by name use ExportTypes.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default; an existing file of the same name is overwritten
- typePath: full path of the PLC data type in the project structure, e.g. 'Group/Name'; call GetTypes to find it

Use the ExportType tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- typePath: {typePath}";
        }

        [McpServerPrompt(Name = "ExportTagTable"), Description("Export one PLC tag table as XML")]
        public static string ExportTagTable(string softwarePath, string exportPath, string tagTablePath)
        {
            return $@"Export exactly one PLC tag table as an XML file. Tag tables have no entry in the export format settings of TIA Portal, so <exportPath>/tia.json is neither read nor created. The file lands in <exportPath>/<softwarePath>/PLC tags/<groups>/, next to the folders of ExportBlock and ExportType. For several tag tables by name use ExportTagTables.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default; an existing file of the same name is overwritten
- tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'; call GetTagTables to find it

Use the ExportTagTable tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- tagTablePath: {tagTablePath}";
        }

        [McpServerPrompt(Name = "ExportWatchTables"), Description("Export PLC watch tables as XML, all or by regular expression")]
        public static string ExportWatchTables(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Export the PLC watch tables of one PLC software as XML files, one per table. Watch tables have no entry in the export format settings of TIA Portal, so <exportPath>/tia.json is neither read nor created. The files land in the watch and force tables folder below <exportPath>/<softwarePath>/, as TIA Portal names it in the current interface language, mirroring the watch table groups. The force table is not included, use ExportForceTable.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten
- regexName: empty for all watch tables, or e.g. '.+HMI' to export only tables whose name matches

Use the ExportWatchTables tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "ExportWatchTable"), Description("Export one PLC watch table as XML")]
        public static string ExportWatchTable(string softwarePath, string exportPath, string watchTablePath)
        {
            return $@"Export exactly one PLC watch table as an XML file. Watch tables have no entry in the export format settings of TIA Portal, so <exportPath>/tia.json is neither read nor created. The file lands in the watch and force tables folder below <exportPath>/<softwarePath>/, below its watch table groups, the same place as with ExportWatchTables. For several watch tables by name use ExportWatchTables.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default; an existing file of the same name is overwritten
- watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'; call GetWatchTables to find it

Use the ExportWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- watchTablePath: {watchTablePath}";
        }

        [McpServerPrompt(Name = "ExportForceTable"), Description("Export the PLC force table as XML")]
        public static string ExportForceTable(string softwarePath, string exportPath, string forceTablePath)
        {
            return $@"Export the PLC force table as an XML file. A PLC has one force table, owned by the system. The file lands in the watch and force tables folder below <exportPath>/<softwarePath>/, next to the watch tables of ExportWatchTables. <exportPath>/tia.json is neither read nor created.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default; an existing file of the same name is overwritten
- forceTablePath: path of the force table as GetForceTables lists it

Use the ExportForceTable tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- forceTablePath: {forceTablePath}";
        }

        [McpServerPrompt(Name = "OverwritePlcProgram"), Description("Overwrite a PLC program with a folder written by ExportPlcProgram")]
        public static string OverwritePlcProgram(string softwarePath, string importPath, string regexName = "", string keepOnError = "false", string dryRun = "true")
        {
            return $@"Use this only when the user explicitly wants the whole program of one PLC software replaced by the files of a folder (first import, restore, a folder without git). For a request to import, preview or apply modified, changed or edited files use ImportPlcProgramChanges instead.

Overwrite the whole program of one PLC software in one call with the files of a folder, the counterpart to ExportPlcProgram (requires the server started with '--allow-write').

Blocks and PLC data types of the same name in the open project are REPLACED by the file content. Always preview first:
1. Call the tool with dryRun true. Nothing is imported; 'Planned' lists every object with 'create' or 'overwrite'
2. Show the list to the user, name the objects that would be overwritten, and ask whether to import
3. Only after the user agrees, call the tool again with dryRun false. The server then opens a confirmation dialog in which the user has to tick 'Yes, import' and press Submit; do not answer it for the user. Without that answer nothing is imported, and a client without such dialogs cannot run the import
4. Do not save on your own. Tell the user the change is only in memory and ask whether to save; call SaveProject (or SaveSession in a local session) only after the user explicitly says 'save project'

What happens:
- .udt files become PLC data types; .db, .awl and .scl files are compiled as sources; .xml files below the 'Program blocks' folder are imported as blocks (LAD); .s7dcl files (SIMATIC SD documents, with their .s7res) below the 'Program blocks' or 'PLC data types' folder are imported as blocks or PLC data types
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
        public static string ImportPlcProgramChanges(string softwarePath, string exportPath, string regexName = "", string keepOnError = "false", string dryRun = "false")
        {
            return $@"This is the default way to import edited files back; for the whole folder use OverwritePlcProgram.

Overwrite objects of one PLC software with only the files of an export folder that git reports as changed since the last commit: modified, staged and new files (requires the server started with '--allow-write', git on PATH and an export folder inside a git repository).

Blocks and PLC data types of the same name in the open project are REPLACED by the file content. The server asks the user itself, so do not ask in the chat first:
1. Call the tool with dryRun false. The server then opens a dialog that lists every file to import and has a 'Yes, import' checkbox and a Submit button; do not answer it for the user. Nothing is imported unless the user ticks the box and submits, and a client without such dialogs cannot run the import
2. If the user only wants to see the list, call the tool with dryRun true instead: nothing is imported and 'Planned' lists every changed file with its object
3. Do not save on your own. Tell the user the change is only in memory and ask whether to save; call SaveProject (or SaveSession in a local session) only after the user explicitly says 'save project'

Typical flow: ExportPlcProgram into the git folder, commit the export as baseline, edit files, then this tool.

What happens:
- Files git reports as deleted cannot be imported and are listed in 'Deleted'; files with an open merge conflict are listed in 'Ignored'
- Formats, groups, rounds, 'Failures' and 'Ignored' work as in OverwritePlcProgram

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: folder that holds the tree ExportPlcProgram wrote and is a git working tree; changes are looked up below <exportPath>/<softwarePath>/
- regexName: empty for all, or e.g. 'FB_.*' to select changed files by base name
- keepOnError: Use false (default) to roll back a whole source file on any error, true to keep the objects of a file that were generated successfully
- dryRun: false (default) opens the confirmation dialog and imports after the user ticks Yes, import; true only lists what would be imported

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
