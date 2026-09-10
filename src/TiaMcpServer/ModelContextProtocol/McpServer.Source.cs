using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Reading what an object contains, without going through the file system.
    ///
    /// Callers: the MCP host, through tool registration. Affected API: none existing - all three
    /// tools are new. File I/O: none that the caller sees; the portal layer exports into a
    /// scratch directory under the temp path and removes it again.
    ///
    /// Read-only: none of these modify the project, so none are gated behind '--allow-write'.
    /// Unlike the Export* tools they are not marked destructive either - they leave nothing on
    /// disk to overwrite.
    /// </summary>
    public static partial class McpServer
    {
        #region source

        [McpServerTool(Name = "GetBlockSource", Title = "Read a block's source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return the source text of one program block directly, instead of exporting a file and reading it back. 'document' gives readable SCL/LAD/STL (SIMATIC Source Document, V20+); objects TIA Portal cannot represent that way - STL and mixed-language blocks - fall back to XML automatically, and the response says which format was produced")]
        public static ResponseSourceText GetBlockSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("format: 'document' (default) for readable source text, or 'xml' for the SimaticML export")] string format = "document",
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetBlockSource(softwarePath, blockPath, format, maxChars),
                blockPath,
                "block");
        }

        [McpServerTool(Name = "GetTypeSource", Title = "Read a PLC data type's source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return the source text of one PLC data type directly, instead of exporting a file and reading it back. 'document' gives the readable TYPE ... END_TYPE declaration (SIMATIC Source Document, requires TIA Portal V21); 'xml' gives the SimaticML export")]
        public static ResponseSourceText GetTypeSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("format: 'document' (default) for readable source text, or 'xml' for the SimaticML export")] string format = "document",
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetTypeSource(softwarePath, typePath, format, maxChars),
                typePath,
                "PLC data type");
        }

        [McpServerTool(Name = "GetBlockInterface", Title = "Read a data block's members", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'GetBlockSource' instead")]
        public static ResponseBlockInterface GetBlockInterface(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the data block, e.g. '1_Tests/DB_Block_1'")] string blockPath)
        {
            try
            {
                var members = Portal.GetBlockInterface(softwarePath, blockPath);

                return new ResponseBlockInterface
                {
                    Message = $"'{blockPath}' has {members.Count} member(s)",
                    Path = blockPath,
                    Items = members.Select(m => new ResponseInterfaceMember
                    {
                        Name = m.Name,
                        DataTypeName = m.DataTypeName,
                        Attributes = m.Attributes
                    }).ToList(),
                    Meta = Ok(new JsonObject { ["memberCount"] = members.Count })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error reading the interface of '{blockPath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportPlcAsSourceTree", Title = "Snapshot a PLC to a source tree", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Write a whole PLC software to one folder tree that mirrors the project, ready to commit: program blocks and PLC data types as readable SIMATIC Source Documents where TIA Portal supports them, tag tables and watch tables as XML, each below its localised system folder. Replaces running the four bulk exports separately. Objects that cannot be exported are reported instead of failing the snapshot")]
        public static ResponseSourceTree ExportPlcAsSourceTree(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten")] string exportPath)
        {
            try
            {
                var result = Portal.ExportPlcAsSourceTree(softwarePath, exportPath);

                return new ResponseSourceTree
                {
                    Message = $"Snapshot of '{softwarePath}' written to '{exportPath}': {result.TotalWritten} object(s), " +
                              $"{result.Skipped.Count} skipped, {result.Failures.Count} failed",
                    Directory = result.Directory,
                    Written = result.Written,
                    Formats = result.Formats,
                    Skipped = result.Skipped,
                    Failures = result.Failures,
                    Meta = Ok(new JsonObject
                    {
                        ["totalWritten"] = result.TotalWritten,
                        ["skipped"] = result.Skipped.Count,
                        ["failed"] = result.Failures.Count
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error snapshotting '{softwarePath}': {ex.Message}", ex);
            }
        }

        /// <summary>Shared response shaping for the two source readers.</summary>
        private static ResponseSourceText Sourced(Func<SourceTextResult> read, string objectPath, string kind)
        {
            try
            {
                var result = read();

                var note = result.Truncated
                    ? $" (truncated to {result.Text.Length} of {result.TotalChars} characters)"
                    : string.Empty;

                return new ResponseSourceText
                {
                    Message = $"Source of {kind} '{objectPath}' as {result.Format}{note}",
                    Name = result.Name,
                    Path = result.Path,
                    Format = result.Format,
                    Text = result.Text,
                    TotalChars = result.TotalChars,
                    Truncated = result.Truncated,
                    FileNames = result.FileNames,
                    Meta = Ok(new JsonObject
                    {
                        ["format"] = result.Format,
                        ["totalChars"] = result.TotalChars,
                        ["truncated"] = result.Truncated
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error reading the source of '{objectPath}': {ex.Message}", ex);
            }
        }

        #endregion
    }
}
