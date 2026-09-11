using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Importing a whole tree of TIA Portal external source files back into the project.
    ///
    /// Callers: the MCP host, through tool registration under '--allow-write'. Affected API:
    /// none existing - the tool is new. File I/O: reads the caller-supplied tree only; the
    /// project change stays in memory until SaveProject/SaveSession, per the SaveHint convention
    /// shared by every tool in McpServerWrite.
    ///
    /// The bulk-import counterpart to 'GenerateSources' (McpServer.GenerateSource.cs): that tool
    /// writes a folder tree of *.db/*.awl/*.scl/*.udt files, this one walks it back into blocks
    /// and PLC data types via 'Portal.ImportSources'.
    /// </summary>
    public static partial class McpServerWrite
    {
        #region import sources

        [McpServerTool(Name = "ImportSources", Title = "Import a tree of source files", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile every block and PLC data type source file (*.db, *.awl, *.scl, *.udt) under a folder tree back into the project - the counterpart to 'GenerateSources'. Each file is placed into the block or PLC data type group its folder path implies, matching the layout 'GenerateSources' writes; a folder whose group does not yet exist in the project fails that file rather than being created automatically. Existing blocks/types of the same name are overwritten")]
        public static ResponseImportedSources ImportSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: directory on this machine to walk recursively for *.db, *.awl, *.scl and *.udt files")] string importPath,
            [Description("regexName: optional regular expression, imports only files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: keep successfully generated objects from a file even when others in it fail; TIA Portal then reports no per-object success/failure for that file. Default false, which rolls back a whole file on any error")] bool keepOnError = false)
        {
            return Guarded(nameof(ImportSources), () =>
            {
                var result = Portal.ImportSources(softwarePath, importPath, regexName, keepOnError);

                return new ResponseImportedSources
                {
                    Directory = result.Directory,
                    Written = result.Written,
                    Items = result.Items.Select(i => new ResponseImportedSourceItem
                    {
                        Name = i.Name,
                        Path = i.Path,
                        Kind = i.Kind
                    }).ToList(),
                    Failures = result.Failures,
                    Message = $"{result.Items.Count} object(s) imported from '{importPath}', {result.Failures.Count} failed. {SaveHint}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["pendingSave"] = true,
                        ["importedCount"] = result.Items.Count,
                        ["failedCount"] = result.Failures.Count,
                        ["keepOnError"] = keepOnError
                    }
                };
            });
        }

        #endregion
    }
}
