using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // From the former McpServer.Source.cs:
        // Reading what an object contains, without going through the file system.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - all three
        // tools are new. File I/O: none that the caller sees; the portal layer exports into a
        // scratch directory under the temp path and removes it again.
        //
        // Read-only: none of these modify the project, so none are gated behind '--allow-write'.
        // Unlike the Export* tools they are not marked destructive either - they leave nothing on
        // disk to overwrite.

        #region source

        [McpServerTool(Name = "GetXmlBlock", Title = "Get block as XML", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return one program block as SimaticML XML text directly, instead of exporting a file and reading it back. Works for every consistent block, whatever its language. The inline counterpart to 'ExportBlock'")]
        public static ResponseSourceText GetXmlBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetXmlBlock(softwarePath, blockPath, maxChars),
                blockPath,
                "block");
        }

        [McpServerTool(Name = "GetSourceBlock", Title = "Get block as source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return one program block as TIA Portal external source text ('.scl' for SCL, '.db' for data blocks, '.awl' for STL) directly, in the format the compiler reads back. LAD, FBD and GRAPH blocks have no source form and are rejected with a reason - use 'GetXmlBlock' or 'GetDocumentsBlock' for those. The inline counterpart to 'ExportBlock'")]
        public static ResponseSourceText GetSourceBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("withDependencies: also include every object the block uses (called blocks, instance DBs, UDTs), so the text compiles on its own. Default false")] bool withDependencies = false,
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetSourceBlock(softwarePath, blockPath, withDependencies, maxChars),
                blockPath,
                "block");
        }

        [McpServerTool(Name = "GetDocumentsBlock", Title = "Get block as documents", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return one program block as readable SIMATIC Source Document text (.s7dcl/.s7res, TIA Portal V20+) directly. STL and mixed-language blocks have no source document and are rejected - use 'GetXmlBlock' or 'GetSourceBlock' for those. The inline counterpart to 'ExportBlock'")]
        public static ResponseSourceText GetDocumentsBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetDocumentsBlock(softwarePath, blockPath, maxChars),
                blockPath,
                "block");
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

        [McpServerTool(Name = "GetXmlType", Title = "Get type as XML", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return one PLC data type as SimaticML XML text directly, instead of exporting a file and reading it back. The inline counterpart to 'ExportType'")]
        public static ResponseSourceText GetXmlType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetXmlType(softwarePath, typePath, maxChars),
                typePath,
                "PLC data type");
        }

        [McpServerTool(Name = "GetSourceType", Title = "Get type as source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return one PLC data type as TIA Portal external source text ('.udt', the TYPE ... END_TYPE declaration) directly, in the format the compiler reads back. Needs no TIA Portal V21. The inline counterpart to 'ExportType'")]
        public static ResponseSourceText GetSourceType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("withDependencies: also include every data type this one uses. Default false")] bool withDependencies = false,
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetSourceType(softwarePath, typePath, withDependencies, maxChars),
                typePath,
                "PLC data type");
        }

        [McpServerTool(Name = "GetDocumentsType", Title = "Get type as documents", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return one PLC data type as readable SIMATIC Source Document text (.s7dcl/.s7res) directly. Requires TIA Portal V21; on older versions use 'GetSourceType' or 'GetXmlType'. The inline counterpart to 'ExportType'")]
        public static ResponseSourceText GetDocumentsType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetDocumentsType(softwarePath, typePath, maxChars),
                typePath,
                "PLC data type");
        }

        #endregion

        // Callers: the export tools in McpServer.Plc.cs and McpServer.Porcelain.cs. File I/O: they
        // write to a caller-supplied directory and overwrite a file of the same name, so they are
        // marked destructive; they do not modify the project, so they are not gated behind
        // '--allow-write'.
        //
        // The response shaping the bulk export tools (ExportPlcProgram, ExportBlocks, ExportTypes,
        // ExportTagTables and the single-object ExportBlock, ExportType, ExportTagTable) share.

        #region export source

        /// <summary>Shared response shaping for the two recursive group generators.</summary>
        private static ResponseGeneratedSources GeneratedMany(Func<GeneratedSourcesResult> generate, string what, string exportPath)
        {
            try
            {
                var result = generate();

                return new ResponseGeneratedSources
                {
                    Message = $"{what} written to '{exportPath}': {result.Files.Count} file(s), " +
                              $"{result.Skipped.Count} skipped, {result.Failures.Count} failed. Show the 'Report' to the user as tables, unchanged." +
                              (result.Notes.Count > 0
                                  ? " " + result.Notes[0] + (result.Notes.Count > 1 ? $" ({result.Notes.Count - 1} more in Notes)" : string.Empty)
                                  : string.Empty),
                    Directory = result.Directory,
                    Written = result.Written,
                    Items = result.Files.Select(ToGeneratedSourceItem).ToList(),
                    Skipped = result.Skipped,
                    Failures = result.Failures,
                    Notes = result.Notes.Count > 0 ? result.Notes : null,
                    Report = ReportFormatter.GeneratedSources(result.Directory, result.Files.Count, result.Written, result.Skipped, result.Failures, result.Notes),
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
                throw new McpException($"Unexpected error exporting {what}: {ex.Message}", ex);
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

        // From the former McpServer.ExternalSources.cs:
        // Read-only MCP tools for PLC external source files.
        //
        // Callers: registered through Program.BuildTools() and invoked directly by the external
        // source test class. Affected API: additive only - a new partial of the existing McpServer
        // type. Data: returns ResponseExternalSource* as MCP structuredContent. No file I/O.
        //
        // PlcExternalSource types only Name, so GetExternalSourceInfo leans on the generic
        // attribute bag for everything else rather than guessing at attribute names.

        #region external sources

        [McpServerTool(Name = "GetExternalSources", Title = "Get external sources", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the external source files of a PLC software, optionally filtered by a regular expression on the source name")]
        public static ResponseExternalSources GetExternalSources(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("regexName: optional regular expression to filter the external source names")] string regexName = "")
        {
            try
            {
                var sources = Portal.GetExternalSources(softwarePath, regexName);

                return new ResponseExternalSources
                {
                    Message = $"{sources.Count} external source(s) retrieved from '{softwarePath}'",
                    Items = sources.Select(ToExternalSourceInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalExternalSources"] = sources.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving external sources from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetExternalSourceInfo", Title = "Get external source info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list")]
        public static ResponseExternalSourceInfo GetExternalSourceInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'")] string sourcePath)
        {
            try
            {
                var source = Portal.GetExternalSource(softwarePath, sourcePath)
                    ?? throw new McpException($"External source not found at '{sourcePath}' in '{softwarePath}'. Use 'GetExternalSources' to list the available sources.");

                var info = ToExternalSourceInfo(source);
                info.Message = $"External source info retrieved from '{sourcePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(source);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving external source info from '{sourcePath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        private static ResponseExternalSourceInfo ToExternalSourceInfo(PlcExternalSource source)
        {
            return new ResponseExternalSourceInfo
            {
                Name = source.Name,
                Path = Portal.GetExternalSourcePath(source)
            };
        }

        #endregion

        // From the former McpServerWrite.Documents.ExternalSources.cs:

        #region external sources (write)

        [WriteTool]
        [McpServerTool(Name = "CreateExternalSourceFromFile", Title = "Create external source from file", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Add a source file (for example an SCL file) from the file system into the external source files of the PLC software")]
        public static ResponseCreated CreateExternalSourceFromFile(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative external source group; empty uses the External source files root")] string groupPath,
            [Description("name: name the source gets in the project, without a slash")] string name,
            [Description("filePath: full path of the source file on the machine running this server")] string filePath)
        {
            return Guarded(nameof(CreateExternalSourceFromFile), () =>
            {
                Portal.CreateExternalSourceFromFile(softwarePath, groupPath, name, filePath);
                return Created("External source", name, JoinPath(groupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteExternalSource", Title = "Delete external source", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Remove an external source file from the PLC software")]
        public static ResponseDeleted DeleteExternalSource(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1")] string sourcePath)
        {
            return Guarded(nameof(DeleteExternalSource), () =>
            {
                Portal.DeleteExternalSource(softwarePath, sourcePath);
                return Deleted("External source", sourcePath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateExternalSourceGroup", Title = "Create external source group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the External source files root of the PLC software")]
        public static ResponseCreated CreateExternalSourceGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below the root")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateExternalSourceGroup), () =>
            {
                Portal.CreateExternalSourceGroup(softwarePath, parentGroupPath, name);
                return Created("External source group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteExternalSourceGroup", Title = "Delete external source group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete an external source group and everything inside it. The External source files system group itself cannot be deleted")]
        public static ResponseDeleted DeleteExternalSourceGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteExternalSourceGroup), () =>
            {
                Portal.DeleteExternalSourceGroup(softwarePath, groupPath);
                return Deleted("External source group", groupPath);
            });
        }

        #endregion
    }
}
