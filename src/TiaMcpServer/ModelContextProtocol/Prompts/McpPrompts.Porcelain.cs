using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
        // The porcelain commands 'tia pull ...' and 'tia push ...': short, git-like names for the
        // export and import tools, see McpServer.Porcelain.cs.

        #region Porcelain Templates

        [McpServerPrompt(Name = "TiaInit"), Description("Set up tia.json with the args and export formats in the export folder")]
        public static string TiaInit(string exportPath = "", string path = "", string softwarePath = "")
        {
            return $@"Set up <exportPath>/tia.json, like 'tia init'. The file holds the export formats of TIA Portal ('version-control-interface'), which the pull tools read, and the 'args' (project, software-path, export-path) that other programs read.

The server opens a dialog of the client in which the user checks or enters the three args and submits; do not answer it for the user. Nothing is written when the user cancels. A client without dialogs uses the parameters below as they are.

What you get: a missing file is created with the export formats TIA Portal offers for a new workspace. An existing file is completed and never overwritten: missing keys are added and every existing value is kept. Tell the user to check the export formats against Options > Settings > Version control interface > Export formats, and pass on which keys were added or kept.

Common parameter values (all optional, they are only the start values of the dialog):
- exportPath: '${{workspacefolder}}/export/Program' is a good default; the file is created there
- path: full path of the .apXX project or .alsXX session file; blank leaves 'project' out of the args
- softwarePath: e.g. 'PLC_1' for hardware PLC; blank leaves 'software-path' out of the args

Use the TiaInit tool with these parameters:
- exportPath: {exportPath}
- path: {path}
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "TiaConnect"), Description("Connect to TIA Portal and open a project or session")]
        public static string TiaConnect(string projectPath, string portalId = "")
        {
            return $@"Connect to TIA Portal and open a project or local session in one step, like 'tia connect <project>'. This is OpenTiaProject under a shorter name and the first step before TiaPull or TiaPush.

What you get: the portalId and the device and PLC software paths. Use a PLC software path as the softwarePath of TiaPull and TiaPush. The project or session that is open in that instance is closed first, unless it is the one asked for.

Common parameter values:
- projectPath: full path of the .apXX project or .alsXX session file on the machine running this server
- portalId: process id of the TIA Portal instance to open it in (see GetPortals); leave empty when at most one instance is running

Use the TiaConnect tool with these parameters:
- path: {projectPath}{OptionalParameter("portalId", portalId)}";
        }

        [McpServerPrompt(Name = "TiaPull"), Description("Pull the whole PLC program from TIA Portal into the export folder")]
        public static string TiaPull(string softwarePath, string exportPath)
        {
            return $@"Pull the whole program of one PLC software out of TIA Portal into the export folder, like 'git pull' updates a working tree. This is ExportPlcProgram under a shorter name.

What you get: every block and PLC data type, each in the format of <exportPath>/tia.json, below <exportPath>/<softwarePath>/Program blocks/... and <exportPath>/<softwarePath>/PLC data types/.... Existing files of the same name are overwritten. If the response has a note about a created tia.json, pass it on to the user. Suggest 'git status' and 'git diff' in the export folder to review the result.

To pull only some objects use TiaPullBlocks, TiaPullTypes or TiaPullTagTables.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Program' is a good default

Use the TiaPull tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}";
        }

        [McpServerPrompt(Name = "TiaPullBlocks"), Description("Pull program blocks, all or by regular expression, into the export folder")]
        public static string TiaPullBlocks(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Pull only the program blocks of one PLC software out of TIA Portal into the export folder, like 'tia pull blocks <regex>'. The files land in the same folders and formats as TiaPull, but no PLC data types are written.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- regexName: empty for all blocks, or e.g. '.+HMI' to pull only blocks whose name matches

Use the TiaPullBlocks tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "TiaPullTypes"), Description("Pull PLC data types, all or by regular expression, into the export folder")]
        public static string TiaPullTypes(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Pull only the PLC data types of one PLC software out of TIA Portal into the export folder, like 'tia pull types <regex>'. The files land in the same folders and format as TiaPull, but no blocks are written.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- regexName: empty for all types, or e.g. '.+HMI' to pull only types whose name matches

Use the TiaPullTypes tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "TiaPullTagTables"), Description("Pull PLC tag tables, all or by regular expression, into the export folder")]
        public static string TiaPullTagTables(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Pull the PLC tag tables of one PLC software out of TIA Portal into the export folder as XML files, like 'tia pull tagtables <regex>'. One file per table is written below <exportPath>/<softwarePath>/PLC tags/<groups>/, next to the folders of TiaPull. TiaPush does not read tag tables back, and there is no tool that imports tag tables.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- regexName: empty for all tag tables, or e.g. '.+HMI' to pull only tables whose name matches

Use the TiaPullTagTables tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "TiaPullBlock"), Description("Pull exactly one program block from TIA Portal into the export folder")]
        public static string TiaPullBlock(string softwarePath, string exportPath, string blockPath)
        {
            return $@"Pull exactly one program block of one PLC software out of TIA Portal into the export folder, like 'tia pull block <path>'. This is ExportBlock under a shorter name: the format is read from <exportPath>/tia.json and the file lands where TiaPull and TiaPullBlocks put it. For several blocks by name use TiaPullBlocks with a regular expression.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- blockPath: full path of the block in the project structure, e.g. 'Group/Subgroup/Name'; call GetBlocks to find it

Use the TiaPullBlock tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "TiaPullType"), Description("Pull exactly one PLC data type from TIA Portal into the export folder")]
        public static string TiaPullType(string softwarePath, string exportPath, string typePath)
        {
            return $@"Pull exactly one PLC data type of one PLC software out of TIA Portal into the export folder, like 'tia pull type <path>'. This is ExportType under a shorter name: the format is read from <exportPath>/tia.json and the file lands where TiaPull and TiaPullTypes put it. For several types by name use TiaPullTypes with a regular expression.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- typePath: full path of the PLC data type in the project structure, e.g. 'Group/Name'; call GetTypes to find it

Use the TiaPullType tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- typePath: {typePath}";
        }

        [McpServerPrompt(Name = "TiaPullTagTable"), Description("Pull exactly one PLC tag table from TIA Portal into the export folder")]
        public static string TiaPullTagTable(string softwarePath, string exportPath, string tagTablePath)
        {
            return $@"Pull exactly one PLC tag table of one PLC software out of TIA Portal into the export folder as an XML file, like 'tia pull tagtable <path>'. This is ExportTagTable under a shorter name; tag tables have a single format, so <exportPath>/tia.json is not used. For several tag tables by name use TiaPullTagTables with a regular expression.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'; call GetTagTables to find it

Use the TiaPullTagTable tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- tagTablePath: {tagTablePath}";
        }

        [McpServerPrompt(Name = "TiaPullWatchTables"), Description("Pull PLC watch tables, all or by regular expression, into the export folder")]
        public static string TiaPullWatchTables(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Pull the PLC watch tables of one PLC software out of TIA Portal into the export folder as XML files, like 'tia pull watchtables <regex>'. One file per table is written into the watch and force tables folder below <exportPath>/<softwarePath>/, mirroring the watch table groups, next to the folders of TiaPull. This is ExportWatchTables under a shorter name. TiaPush does not read watch tables back.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- regexName: empty for all watch tables, or e.g. '.+HMI' to pull only tables whose name matches

Use the TiaPullWatchTables tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "TiaPullWatchTable"), Description("Pull exactly one PLC watch table from TIA Portal into the export folder")]
        public static string TiaPullWatchTable(string softwarePath, string exportPath, string watchTablePath)
        {
            return $@"Pull exactly one PLC watch table of one PLC software out of TIA Portal into the export folder as an XML file, like 'tia pull watchtable <path>'. This is ExportWatchTable under a shorter name; watch tables have a single format, so <exportPath>/tia.json is not used. For several watch tables by name use TiaPullWatchTables with a regular expression.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'; call GetWatchTables to find it

Use the TiaPullWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- watchTablePath: {watchTablePath}";
        }

        [McpServerPrompt(Name = "TiaPullForceTable"), Description("Pull the PLC force table from TIA Portal into the export folder")]
        public static string TiaPullForceTable(string softwarePath, string exportPath, string forceTablePath)
        {
            return $@"Pull the PLC force table of one PLC software out of TIA Portal into the export folder as an XML file, like 'tia pull forcetable <path>'. This is ExportForceTable under a shorter name; a PLC has one force table, call GetForceTables to find its name. The file lands in the watch and force tables folder, next to the watch tables.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: '${{workspacefolder}}/export/Program' is a good default
- forceTablePath: path of the force table as GetForceTables lists it

Use the TiaPullForceTable tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- forceTablePath: {forceTablePath}";
        }

        [McpServerPrompt(Name = "TiaPush"),Description("Push the files git reports as changed back into TIA Portal")]
        public static string TiaPush(string softwarePath, string exportPath, string regexName = "", string keepOnError = "false")
        {
            return $@"Push the files of an export folder that git reports as changed (modified, staged and new) back into TIA Portal, like 'git push' sends local commits on. This is ImportPlcProgramChanges with dryRun false under a shorter name (requires the server started with '--allow-write', git on PATH and an export folder inside a git repository).

Blocks and PLC data types of the same name in the open project are REPLACED by the file content. The server asks the user itself, so do not ask in the chat first:
1. Call the tool. The server opens a dialog that lists every file to import and has a 'Yes, import' checkbox and a Submit button; do not answer it for the user. Nothing is imported unless the user ticks the box and submits, and a client without such dialogs cannot run the push; use TiaPushPreview there
2. Do not save on your own. Tell the user the change is only in memory and ask whether to save; call SaveProject (or SaveSession in a local session) only after the user explicitly says 'save project'

Typical flow: TiaPull into the git folder, commit the pull as baseline, edit files, then this tool.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: folder that holds the tree TiaPull wrote and is a git working tree; changes are looked up below <exportPath>/<softwarePath>/
- regexName: empty for all, or e.g. 'FB_.*' to push only changed files whose base name matches
- keepOnError: Use false (default) to roll back a whole source file on any error, true to keep the objects of a file that were generated successfully

Use the TiaPush tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- keepOnError: {NormalizeBool(keepOnError)}";
        }

        [McpServerPrompt(Name = "TiaPushPreview"), Description("Show what a push would import, without importing anything")]
        public static string TiaPushPreview(string softwarePath, string exportPath, string regexName = "", string keepOnError = "false")
        {
            return $@"Show what TiaPush would put into TIA Portal, without importing anything, like 'tia push preview'. This is ImportPlcProgramChanges with dryRun true under a shorter name (requires the server started with '--allow-write', git on PATH and an export folder inside a git repository).

'Table' is a markdown table of the affected files with the columns Git, Type and File (files git reports as deleted have the status D and cannot be imported); 'Planned' has the details per file, 'Ignored' lists skipped files. Show the 'Table' to the user as a table, unchanged. Name the objects that would be overwritten, and call TiaPush only when the user asks for it.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- exportPath: folder that holds the tree TiaPull wrote and is a git working tree; changes are looked up below <exportPath>/<softwarePath>/
- regexName: empty for all, or e.g. 'FB_.*' to preview only changed files whose base name matches
- keepOnError: only echoed in the response of a preview; leave it at false

Use the TiaPushPreview tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- keepOnError: {NormalizeBool(keepOnError)}";
        }

        #endregion
    }
}
