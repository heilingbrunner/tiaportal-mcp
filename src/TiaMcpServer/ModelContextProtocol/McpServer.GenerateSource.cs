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
    /// Writing TIA Portal external source files (*.scl, *.db, *.awl, *.udt).
    ///
    /// Callers: the MCP host, through tool registration. Affected API: none existing - all three
    /// tools are new.
    ///
    /// File I/O: these write to a caller-supplied directory and overwrite a file of the same
    /// name, so they are marked destructive, exactly like the Export* tools. They do not modify
    /// the project, so they are not gated behind '--allow-write'.
    ///
    /// Why this sits next to the existing exporters rather than replacing them: 'ExportBlock'
    /// writes SimaticML, 'ExportAsDocuments' writes SIMATIC Source Documents, and neither can be
    /// compiled back. These files can - 'CreateExternalSourceFromFile' plus
    /// 'GenerateBlocksFromSource' is the return path.
    /// </summary>
    public static partial class McpServer
    {
        #region generate source

        [McpServerTool(Name = "GenerateBlockSource", Title = "Generate a block's source file", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Write one program block as a TIA Portal external source file, in the format the compiler reads back. The extension follows the block: '.db' for data blocks, '.scl' for SCL blocks, '.awl' for STL blocks. Only those three can be generated - LAD, FBD and GRAPH blocks have no source form and are rejected with a reason, for which 'ExportBlock' (SimaticML) or 'ExportAsDocuments' is the alternative")]
        public static ResponseGeneratedSource GenerateBlockSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("exportPath: directory on this machine that receives the file; an existing file of the same name is overwritten")] string exportPath,
            [Description("withDependencies: also write every object the block uses (called blocks, instance DBs, UDTs) into the same file, so it compiles on its own. Default false")] bool withDependencies = false,
            [Description("preservePath: mirror the project groups below '<exportPath>/Program blocks', as the Export* tools do. Default false, which writes straight into exportPath")] bool preservePath = false)
        {
            return Generated(
                () => Portal.GenerateBlockSource(softwarePath, blockPath, exportPath, withDependencies, preservePath),
                blockPath,
                "block");
        }

        [McpServerTool(Name = "GenerateTypeSource", Title = "Generate a PLC data type's source file", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Write one PLC data type as a '*.udt' external source file, in the format the compiler reads back. Unlike 'GetTypeSource' this needs no TIA Portal V21 and produces a file that can be imported again")]
        public static ResponseGeneratedSource GenerateTypeSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("exportPath: directory on this machine that receives the file; an existing file of the same name is overwritten")] string exportPath,
            [Description("withDependencies: also write every data type this one uses into the same file. Default false")] bool withDependencies = false,
            [Description("preservePath: mirror the project groups below '<exportPath>/PLC data types', as the Export* tools do. Default false, which writes straight into exportPath")] bool preservePath = false)
        {
            return Generated(
                () => Portal.GenerateTypeSource(softwarePath, typePath, exportPath, withDependencies, preservePath),
                typePath,
                "PLC data type");
        }

        [McpServerTool(Name = "GenerateSources", Title = "Generate sources for a whole PLC", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Write every block and PLC data type of one PLC software as external source files into a folder tree that mirrors the project groups: '<exportPath>/Program blocks/...' and '<exportPath>/PLC data types/...', one file per object. The compilable counterpart to 'ExportPlcAsSourceTree'. Objects with no source form (LAD, FBD, GRAPH), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run")]
        public static ResponseGeneratedSources GenerateSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten")] string exportPath,
            [Description("regexName: optional regular expression, generates only objects whose name matches. Empty means all")] string regexName = "",
            [Description("withDependencies: also write every object each one uses into its file. Default false, which keeps one object per file")] bool withDependencies = false)
        {
            try
            {
                var result = Portal.GenerateSources(softwarePath, exportPath, regexName, withDependencies);

                return new ResponseGeneratedSources
                {
                    Message = $"Sources of '{softwarePath}' written to '{exportPath}': {result.Files.Count} file(s), " +
                              $"{result.Skipped.Count} skipped, {result.Failures.Count} failed",
                    Directory = result.Directory,
                    Written = result.Written,
                    Items = result.Files.Select(ToGeneratedSourceItem).ToList(),
                    Skipped = result.Skipped,
                    Failures = result.Failures,
                    Meta = Ok(new JsonObject
                    {
                        ["totalWritten"] = result.Files.Count,
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
                throw new McpException($"Unexpected error generating sources for '{softwarePath}': {ex.Message}", ex);
            }
        }

        /// <summary>Shared response shaping for the two single-object generators.</summary>
        private static ResponseGeneratedSource Generated(Func<GeneratedSource> generate, string objectPath, string kind)
        {
            try
            {
                var result = generate();

                return new ResponseGeneratedSource
                {
                    Message = $"Source of {kind} '{objectPath}' written to '{result.File}'",
                    Name = result.Name,
                    Path = result.Path,
                    File = result.File,
                    Format = result.Format,
                    Language = result.Language,
                    Size = result.Size,
                    Meta = Ok(new JsonObject
                    {
                        ["format"] = result.Format,
                        ["size"] = result.Size
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error generating the source of '{objectPath}': {ex.Message}", ex);
            }
        }

        private static ResponseGeneratedSourceItem ToGeneratedSourceItem(GeneratedSource source) => new ResponseGeneratedSourceItem
        {
            Name = source.Name,
            Path = source.Path,
            File = source.File,
            Format = source.Format,
            Language = source.Language,
            Size = source.Size
        };

        #endregion
    }
}
