using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Import of PLC data types from SIMATIC Source Documents.
    ///
    /// Callers: the MCP host, but only when the server runs with '--allow-write'. Affected API:
    /// none existing - both tools are new. File I/O: reads the caller's '.s7dcl'/'.s7res' files;
    /// writes none.
    ///
    /// These live in the write tool type, unlike the older block tools ImportFromDocuments and
    /// ImportBlocksFromDocuments, which sit in the ungated McpServer even though they also
    /// create objects in the project. That is a known inconsistency in the block tools, not a
    /// pattern to copy: WritePolicy states that only filesystem-only exports stay ungated.
    /// </summary>
    public static partial class McpServerWrite
    {
        #region type documents

        [McpServerTool(Name = "ImportTypeFromDocuments", Title = "Import PLC data type from documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import one PLC data type from a SIMATIC Source Document set (.s7dcl plus an optional .s7res) on the file system of the machine running this server. A PLC data type name is unique across the whole PLC, so importing an existing name into a different group fails even with importOption 'Override' - target the group the type already lives in. Requires TIA Portal V21 or newer")]
        public static ResponseImportTypeFromDocuments ImportTypeFromDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative PLC data type group that receives the type; empty uses the PLC data types root. A leading 'PLC data types' segment, as written by preservePath exports, is accepted and ignored")] string groupPath,
            [Description("importPath: directory containing the document files")] string importPath,
            [Description("fileNameWithoutExtension: base name of the document set, e.g. 'BtnTyp_X'")] string fileNameWithoutExtension,
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            return Guarded(nameof(ImportTypeFromDocuments), () =>
            {
                var option = McpServer.ParseImportDocumentOption(importOption);
                var warnings = McpServer.GetResMissingEnUsIds(importPath, fileNameWithoutExtension);

                var imported = Portal.ImportTypeFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, option);

                return new ResponseImportTypeFromDocuments
                {
                    Message = $"PLC data type '{fileNameWithoutExtension}' imported from '{importPath}' into '{TargetGroupText(groupPath)}'. {SaveHint}",
                    Items = imported.Select(McpServer.ToTypeInfo).ToList(),
                    Meta = ImportMeta(imported.Count, warnings)
                };
            });
        }

        [McpServerTool(Name = "ImportTypesFromDocuments", Title = "Import PLC data types from documents", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import every SIMATIC Source Document set in a directory as a PLC data type. A set that fails does not abort the run. A PLC data type name is unique across the whole PLC, so a name that already exists in another group fails even with importOption 'Override'. Requires TIA Portal V21 or newer")]
        public static async Task<ResponseImportTypesFromDocuments> ImportTypesFromDocuments(
            IProgress<ProgressNotificationValue> progress,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative PLC data type group that receives the types; empty uses the PLC data types root. A leading 'PLC data types' segment is accepted and ignored")] string groupPath,
            [Description("importPath: directory containing the document files")] string importPath,
            [Description("regexName: name or regular expression selecting the document sets. Use empty string (default) to import all")] string regexName = "",
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            WritePolicy.EnsureEnabled(nameof(ImportTypesFromDocuments));

            var option = McpServer.ParseImportDocumentOption(importOption);

            progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Importing PLC data types from '{importPath}'..." });

            try
            {
                var imported = await Task.Run(
                    () => Portal.ImportTypesFromDocuments(softwarePath, groupPath, importPath, regexName, option));

                progress.Report(new ProgressNotificationValue
                {
                    Progress = imported.Count,
                    Total = imported.Count,
                    Message = $"Imported {imported.Count} PLC data types from documents"
                });

                return new ResponseImportTypesFromDocuments
                {
                    Message = $"{imported.Count} PLC data types imported from '{importPath}' into '{TargetGroupText(groupPath)}'. {SaveHint}",
                    Items = imported.Select(McpServer.ToTypeInfo).ToList(),
                    Meta = ImportMeta(imported.Count, new List<string>())
                };
            }
            catch (PortalException pex)
            {
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Import failed: {pex.Message}" });

                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Import failed: {ex.Message}" });

                throw new McpException($"Unexpected error importing PLC data types from '{importPath}': {ex.Message}", ex);
            }
        }

        #endregion

        #region helpers

        private static string TargetGroupText(string groupPath) =>
            string.IsNullOrEmpty(groupPath) ? "the PLC data types root" : groupPath;

        /// <summary>
        /// Adds the imported count and, when a '.s7res' lacks en-US entries, the ids that may
        /// make an import fail - the same pre-check the block import tools report.
        /// </summary>
        private static JsonObject ImportMeta(int importedCount, List<string> warnings)
        {
            var meta = OkMeta();

            meta["importedTypes"] = importedCount;

            if (warnings.Count > 0)
            {
                meta["warnings"] = new JsonArray(warnings.Select(w => (JsonNode?)w).ToArray());
            }

            return meta;
        }

        #endregion
    }
}
