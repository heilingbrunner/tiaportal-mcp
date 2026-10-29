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
        // The whole program of one PLC in one call: ExportPlcProgram writes it, ImportPlcProgram
        // reads it back. Source files for SCL, STL, data blocks and PLC data types, XML for LAD.
        //
        // Callers: the MCP host, through tool registration; ImportPlcProgram only under
        // '--allow-write'. Affected API: none existing - both tools are new. File I/O: a folder
        // tree below '<exportPath>/<softwarePath>/', the same layout ExportSources and
        // ExportXmlBlocks write with preservePath, so the narrower tools can work on the same folder.
        //
        // 'exportPath' and 'softwarePath' are the argument names the command line presets fill in
        // ('--export-path', '--software-path'), so these tools take them like every other export.

        #region plc program

        [McpServerTool(Name = "ExportPlcProgram", Title = "Export PLC program", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export a whole PLC program in one call: every block and PLC data type of a PLC software, each in the format that suits it. SCL and STL blocks, data blocks and PLC data types are written as source files (.scl, .awl, .db, .udt) and LAD blocks as XML (.xml), into '<exportPath>/<softwarePath>/Program blocks/...' and '<exportPath>/<softwarePath>/PLC data types/...', mirroring the project groups, one file per object. Blocks in other languages (FBD, GRAPH, ...), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run; export those with 'ExportXmlBlock' or 'ExportAsDocuments'. 'ImportPlcProgram' reads the folder back. For only sources use 'ExportSources', for only XML use 'ExportXmlBlocks'")]
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
        [McpServerTool(Name = "ImportPlcProgram", Title = "Import PLC program", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to import a whole PLC program in one call, the counterpart to 'ExportPlcProgram': .udt files become PLC data types, .db, .awl and .scl files are compiled as sources, and .xml files below the 'Program blocks' folder are imported as blocks (LAD). Each file goes into the block or PLC data type group its folder path implies; a group that does not exist makes that file fail instead of being created. Existing blocks and types of the same name are overwritten. Files are imported in rounds: an object that needs another one (a LAD block calling an SCL block, an instance data block) is retried until nothing more can be imported; 'Rounds' says how many passes were made and 'Failures' lists what never succeeded. Other files, and .xml files outside the 'Program blocks' folder, are listed in 'Ignored'. For only sources use 'ImportSources', for one XML file use 'ImportXmlBlock'")]
        public static ResponseImportedProgram ImportPlcProgram(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("importPath: directory on this machine that holds the tree ExportPlcProgram wrote; read recursively from <importPath>/<softwarePath>/")] string importPath,
            [Description("regexName: optional regular expression, imports only files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: applies to source files only: keep the objects of a file that were generated successfully even when others in it fail. Default false, which rolls back a whole file on any error. XML files are never partial")] bool keepOnError = false)
        {
            return Guarded(nameof(ImportPlcProgram), () =>
                ImportedProgram(Portal.ImportPlcProgram(softwarePath, importPath, regexName, keepOnError), importPath, keepOnError));
        }

        private static ResponseImportedProgram ImportedProgram(PlcProgramImportResult result, string importPath, bool keepOnError)
        {
            return new ResponseImportedProgram
            {
                Directory = result.Directory,
                Written = result.Written,
                Items = result.Items.Select(i => new ResponseImportedProgramItem
                {
                    Name = i.Name,
                    Path = i.Path,
                    Kind = i.Kind,
                    Format = i.Format
                }).ToList(),
                Failures = result.Failures,
                Ignored = result.Ignored,
                Rounds = result.Rounds,
                Message = $"{result.Items.Count} object(s) imported from '{importPath}' in {result.Rounds} round(s), {result.Failures.Count} failed. {SaveHint}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["pendingSave"] = true,
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
