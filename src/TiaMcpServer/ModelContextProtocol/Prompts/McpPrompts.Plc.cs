using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
        #region PLC Program Templates

        [McpServerPrompt(Name = "ExportPlcProgram"), Description("Export all blocks and PLC data types: sources, and XML for LAD blocks")]
        public static string ExportPlcProgram(string softwarePath, string exportPath, string regexName = "")
        {
            return $@"Export the whole program of one PLC software in one call, each object in the format that suits it.

What you get:
- .scl for SCL blocks, .awl for STL blocks, .db for data blocks and .udt for PLC data types, as external source files
- .xml for LAD blocks, because TIA Portal generates no source for them
- Blocks in other languages (FBD, GRAPH, ...), inconsistent and know-how protected objects are listed in 'Skipped'; use ExportXmlBlock or ExportAsDocuments for those
- Files are written below <exportPath>/<softwarePath>/Program blocks/... and <exportPath>/<softwarePath>/PLC data types/..., mirroring the project groups. ImportPlcProgram reads the folder back

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Program' is a good default; existing files of the same name are overwritten
- regexName: Use empty string """" for all objects, or a pattern like ""FB_.*"" to export only matching names

Use the ExportPlcProgram tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "ImportPlcProgram"), Description("Import a folder written by ExportPlcProgram (sources and XML)")]
        public static string ImportPlcProgram(string softwarePath, string importPath, string regexName = "", string keepOnError = "false")
        {
            return $@"Import the whole program of one PLC software in one call, the counterpart to ExportPlcProgram (requires the server started with '--allow-write').

What happens:
- .udt files become PLC data types; .db, .awl and .scl files are compiled as sources; .xml files below the 'Program blocks' folder are imported as blocks (LAD)
- Each file goes into the block or PLC data type group its folder path implies; the group must already exist (create it with CreateBlockGroup or CreateTypeGroup), otherwise that file fails
- Existing blocks and types of the same name are overwritten
- Files are imported in rounds: an object that needs another one (a LAD block calling an SCL block, an instance data block) is retried until nothing more can be imported; the response shows 'Rounds' and lists what never succeeded in 'Failures'
- Other files, and .xml files outside the 'Program blocks' folder, are listed in 'Ignored'
- Afterwards call SaveProject (or SaveSession) to persist the change

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- importPath: folder that holds the tree ExportPlcProgram wrote; read from <importPath>/<softwarePath>/
- regexName: empty for all, or e.g. 'FB_.*' to select files by base name
- keepOnError: Use false (default) to roll back a whole source file on any error, true to keep the objects of a file that were generated successfully

Use the ImportPlcProgram tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- regexName: {regexName}
- keepOnError: {NormalizeBool(keepOnError)}";
        }

        #endregion
    }
}
