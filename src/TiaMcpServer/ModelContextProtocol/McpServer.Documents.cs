using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Types;
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
    /// Export of PLC data types as SIMATIC Source Documents - the readable, git-diffable form of
    /// a type, as opposed to the SimaticML XML that ExportType writes.
    ///
    /// Callers: the MCP host, through tool registration. Affected API: none existing - both tools
    /// are new. File I/O: TIA Portal writes one document set per type below the caller's
    /// exportPath; the response names the files it actually produced.
    ///
    /// Not gated behind '--allow-write': these tools only write to the file system and never
    /// modify the project. The matching imports do modify it and live in
    /// McpServerWrite.Documents.cs.
    /// </summary>
    public static partial class McpServer
    {
        #region type documents

        [McpServerTool(Name = "ExportTypeAsDocuments", Title = "Export PLC data type as documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export one PLC data type as a SIMATIC Source Document set (.s7dcl plus an optional .s7res) instead of XML. The response lists the files TIA Portal actually wrote. Requires TIA Portal V21 or newer")]
        public static ResponseExportTypeAsDocuments ExportTypeAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'GetTypes' to list them")] string typePath,
            [Description("exportPath: directory on this machine that receives the document files")] string exportPath,
            [Description("preservePath: recreate the group structure below exportPath, inside the 'PLC data types' system folder as TIA Portal names it in the current interface language")] bool preservePath = false)
        {
            try
            {
                var info = Portal.ExportTypeAsDocuments(softwarePath, typePath, exportPath, preservePath);
                var type = Portal.GetType(softwarePath, typePath);

                return new ResponseExportTypeAsDocuments
                {
                    Message = $"PLC data type '{typePath}' exported as documents to '{info.Directory}'",
                    Item = type == null ? null : ToTypeInfo(type),
                    Documents = ToDocumentFiles(info),
                    Meta = Ok(new JsonObject { ["files"] = info.Files.Count, ["state"] = info.State })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting '{typePath}' as documents: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportTypesAsDocuments", Title = "Export PLC data types as documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export PLC data types as SIMATIC Source Document sets (.s7dcl plus optional .s7res) instead of XML. Inconsistent types are skipped and reported, and a type TIA Portal cannot export does not abort the run. Requires TIA Portal V21 or newer")]
        public static async Task<ResponseExportTypesAsDocuments> ExportTypesAsDocuments(
            IProgress<ProgressNotificationValue> progress,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the document files")] string exportPath,
            [Description("regexName: name or regular expression selecting the types. Use empty string (default) to export all")] string regexName = "",
            [Description("preservePath: recreate the group structure below exportPath, inside the 'PLC data types' system folder as TIA Portal names it in the current interface language")] bool preservePath = false)
        {
            var startTime = DateTime.Now;

            try
            {
                Logger?.LogInformation("Starting export of PLC data types as documents from '{SoftwarePath}' to '{ExportPath}'", softwarePath, exportPath);

                var total = (await Task.Run(() => Portal.GetTypes(softwarePath, regexName))).Count;

                if (total == 0)
                {
                    progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = "No PLC data types found to export as documents" });

                    return new ResponseExportTypesAsDocuments
                    {
                        Message = $"No PLC data types found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseTypeInfo>(),
                        Documents = new List<ResponseDocumentFiles>(),
                        Inconsistent = new List<ResponseTypeInfo>(),
                        Failures = new List<string>(),
                        Meta = Ok(new JsonObject
                        {
                            ["totalTypes"] = 0,
                            ["exportedTypes"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        })
                    };
                }

                progress.Report(new ProgressNotificationValue { Progress = 0, Total = total, Message = $"Starting export of {total} PLC data types as documents..." });

                var outcome = await Task.Run(() => Portal.ExportTypesAsDocuments(softwarePath, exportPath, regexName, preservePath));

                var exported = outcome.Exported.Count;

                progress.Report(new ProgressNotificationValue { Progress = exported, Total = total, Message = $"Document export completed: {exported} of {total} PLC data types exported" });

                var duration = (DateTime.Now - startTime).TotalSeconds;

                Logger?.LogInformation("Document export completed: {Count} PLC data types in {Duration:F2} seconds", exported, duration);

                return new ResponseExportTypesAsDocuments
                {
                    Message = $"Document export completed: {exported} PLC data types with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                    Items = outcome.Exported.Where(e => e.Type != null).Select(e => ToTypeInfo(e.Type!)).ToList(),
                    Documents = outcome.Exported.Select(e => ToDocumentFiles(e.Documents)).ToList(),
                    Inconsistent = outcome.Inconsistent.Select(ToTypeInfo).ToList(),
                    Failures = outcome.Failures,
                    Meta = Ok(new JsonObject
                    {
                        ["totalTypes"] = total,
                        ["exportedTypes"] = exported,
                        ["inconsistentTypes"] = outcome.Inconsistent.Count,
                        ["failedTypes"] = outcome.Failures.Count,
                        ["duration"] = duration
                    })
                };
            }
            catch (PortalException pex)
            {
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Document export failed: {pex.Message}" });

                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Document export failed: {ex.Message}" });

                throw new McpException($"Unexpected error exporting PLC data types as documents to '{exportPath}': {ex.Message}", ex);
            }
        }

        #endregion

        #region mapping

        internal static ResponseDocumentFiles ToDocumentFiles(DocumentExportInfo info) => new ResponseDocumentFiles
        {
            Name = info.Name,
            Directory = info.Directory,
            Files = info.Files,
            Messages = info.Messages,
            State = info.State
        };

        internal static ResponseTypeInfo ToTypeInfo(PlcType type) => new ResponseTypeInfo
        {
            Name = type.Name,
            TypeName = type.GetType().Name,
            Namespace = type.Namespace,
            IsConsistent = type.IsConsistent,
            ModifiedDate = type.ModifiedDate,
            IsKnowHowProtected = type.IsKnowHowProtected,
            Attributes = Helper.GetAttributeList(type),
            Description = type.ToString()
        };

        #endregion
    }
}
