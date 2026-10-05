using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // From the former McpServer.Documents.cs:
        // Export of PLC data types as SIMATIC Source Documents - the readable, git-diffable form of
        // a type, as opposed to the SimaticML XML that ExportXmlType writes.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - both tools
        // are new. File I/O: TIA Portal writes one document set per type below the caller's
        // exportPath; the response names the files it actually produced.
        //
        // Not gated behind '--allow-write': these tools only write to the file system and never
        // modify the project. The matching imports do modify it and live in
        // the "type documents (write)" region further down in this file.

        #region type documents

        [McpServerTool(Name = "ExportTypeAsDocuments", Title = "Export type as documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export type as documents (.s7dcl plus an optional .s7res)")]
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

        [McpServerTool(Name = "ExportTypesAsDocuments", Title = "Export types as documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export types as documents (.s7dcl plus optional .s7res)")]
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
                    Items = outcome.Exported.Where(e => e.Type is not null).Select(e => ToTypeInfo(e.Type!)).ToList(),
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
            Path = Portal.GetTypePath(type),
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

        // From the former McpServerWrite.Documents.cs:
        // Import of PLC data types from SIMATIC Source Documents.
        //
        // Callers: the MCP host, but only when the server runs with '--allow-write'. Affected API:
        // none existing - both tools are new. File I/O: reads the caller's '.s7dcl'/'.s7res' files;
        // writes none.
        //
        // These live in the write tool type, unlike the older block tools ImportFromDocuments and
        // ImportBlocksFromDocuments, which sit in the ungated McpServer even though they also
        // create objects in the project. That is a known inconsistency in the block tools, not a
        // pattern to copy: WritePolicy states that only filesystem-only exports stay ungated.

        #region type documents (write)

        [WriteTool]
        [McpServerTool(Name = "ImportTypeFromDocuments", Title = "Import type from documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import type from documents (.s7dcl plus an optional .s7res)")]
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

        [WriteTool]
        [McpServerTool(Name = "ImportTypesFromDocuments", Title = "Import types from documents", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import types from documents (.s7dcl plus an optional .s7res)")]
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

        #region helpers (write)

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

        #region documents

        [McpServerTool(Name = "ExportAsDocuments", Title = "Export as documents", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export as documents (.s7dcl/.s7res) from a block in the plc software to path")]
        public static ResponseExportAsDocuments ExportAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportAsDocuments requires TIA Portal V20 or newer");
                }
                if (Portal.ExportAsDocuments(softwarePath, blockPath, exportPath, preservePath))
                {
                    return new ResponseExportAsDocuments
                    {
                        Message = $"Documents exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents from '{blockPath}' to '{exportPath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting documents from '{blockPath}' to '{exportPath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportBlocksAsDocuments", Title = "Export blocks as documents", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export as documents (.s7dcl/.s7res) from blocks in the plc software to path")]
        public static async Task<ResponseExportBlocksAsDocuments> ExportBlocksAsDocuments(
            IProgress<ProgressNotificationValue> progress,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportBlocksAsDocuments requires TIA Portal V20 or newer");
                }
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks as documents from '{softwarePath}' to '{exportPath}'");
                
                var allBlocks = await Task.Run(() => Portal.GetBlocks(softwarePath, regexName));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = "No blocks found to export as documents" });
                    
                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = totalBlocks, Message = $"Starting export of {totalBlocks} blocks as documents..." });

                // Export blocks as documents asynchronously
                var exportedBlocks = await Task.Run(() => Portal.ExportBlocksAsDocuments(softwarePath, exportPath, regexName, preservePath));
                
                // Send progress update after export completion
                if (exportedBlocks != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    progress.Report(new ProgressNotificationValue { Progress = exportedCount, Total = totalBlocks, Message = $"Exported {exportedCount} of {totalBlocks} blocks as documents" });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);

                            responseList.Add(new ResponseBlockInfo
                            {
                                Path = Portal.GetBlockPath(block),
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    progress.Report(new ProgressNotificationValue { Progress = processedCount, Total = totalBlocks, Message = $"Document export completed: {processedCount} blocks exported successfully" });

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Document export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"Document export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents to '{exportPath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Document export failed: {ex.Message}" });
                
                Logger?.LogError(ex, $"Failed exporting documents to '{exportPath}'");
                throw new McpException($"Unexpected error exporting documents to '{exportPath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ImportFromDocuments", Title = "Import from documents", Destructive = true, Idempotent = true, OpenWorld = false), Description("Import from documents (.s7dcl/.s7res)")]
        public static ResponseImportFromDocuments ImportFromDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the block should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("fileNameWithoutExtension: name of the block file without extension") ] string fileNameWithoutExtension,
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportFromDocuments requires TIA Portal V20 or newer");
                }

                var option = ParseImportDocumentOption(importOption);

                // Pre-check .s7res for missing en-US tags
                var warnings = new JsonArray();
                try
                {
                    var missingIds = GetResMissingEnUsIds(importPath, fileNameWithoutExtension);
                    if (missingIds != null && missingIds.Count > 0)
                    {
                        Logger?.LogWarning($".s7res for '{fileNameWithoutExtension}' missing en-US tags for {missingIds.Count} items: {string.Join(", ", missingIds)}");
                        warnings.Add(new JsonObject
                        {
                            ["name"] = fileNameWithoutExtension,
                            ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to evaluate .s7res warnings");
                }

                var ok = Portal.ImportFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, option);
                if (ok)
                {
                    return new ResponseImportFromDocuments
                    {
                        Message = $"Imported '{fileNameWithoutExtension}' from '{importPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["warnings"] = warnings
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing '{fileNameWithoutExtension}' from '{importPath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing from documents: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ImportBlocksFromDocuments", Title = "Import blocks from documents", Destructive = true, Idempotent = true, OpenWorld = false), Description("Import blocks from documents (.s7dcl/.s7res)")]
        public static async Task<ResponseImportBlocksFromDocuments> ImportBlocksFromDocuments(
            IProgress<ProgressNotificationValue> progress,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the blocks should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("regexName: name or regular expression to select block files (empty for all)")] string regexName = "",
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            var startTime = DateTime.Now;

            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportBlocksFromDocuments requires TIA Portal V20 or newer");
                }

                // Determine total by scanning .s7dcl files matching regex
                int total = 0;
                var scanWarnings = new JsonArray();
                try
                {
                    if (Directory.Exists(importPath))
                    {
                        var rx = string.IsNullOrWhiteSpace(regexName) ? null : new Regex(regexName, RegexOptions.Compiled);
                        var files = Directory.GetFiles(importPath, "*.s7dcl", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(f);
                            if (rx != null && !rx.IsMatch(name))
                                continue;
                            total++;

                            try
                            {
                                var missingIds = GetResMissingEnUsIds(importPath, name);
                                if (missingIds != null && missingIds.Count > 0)
                                {
                                    scanWarnings.Add(new JsonObject
                                    {
                                        ["name"] = name,
                                        ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { /* ignore pre-scan errors */ }

                progress.Report(new ProgressNotificationValue { Progress = 0, Total = total, Message = total > 0 ? $"Starting import of {total} blocks from documents..." : "Scanning import directory..." });

                var option = ParseImportDocumentOption(importOption);
                var imported = await Task.Run(() => Portal.ImportBlocksFromDocuments(softwarePath, groupPath, importPath, regexName, option));

                var responseList = new List<ResponseBlockInfo>();
                int processed = 0;
                if (imported != null)
                {
                    foreach (var block in imported)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);
                            responseList.Add(new ResponseBlockInfo
                            {
                                Path = Portal.GetBlockPath(block),
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processed++;
                    }
                }

                progress.Report(new ProgressNotificationValue { Progress = processed, Total = total, Message = $"Document import completed: {processed} blocks imported successfully" });

                var duration = (DateTime.Now - startTime).TotalSeconds;
                Logger?.LogInformation($"Document import completed: {processed} blocks imported in {duration:F2} seconds");

                return new ResponseImportBlocksFromDocuments
                {
                    Message = $"Document import completed: {processed} blocks imported from '{importPath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["totalBlocks"] = total,
                        ["importedBlocks"] = processed,
                        ["duration"] = duration,
                        ["warnings"] = scanWarnings
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Document import failed: {ex.Message}" });

                Logger?.LogError(ex, $"Failed importing documents from '{importPath}'");
                throw new McpException($"Unexpected error importing documents from '{importPath}': {ex.Message}", ex);
            }
        }

        /// <summary>Internal, not private: the import tools in this file parse the same option.</summary>
        internal static ImportDocumentOptions ParseImportDocumentOption(string option)
        {
            if (string.IsNullOrWhiteSpace(option)) return ImportDocumentOptions.Override;

            var normalized = option.Trim();

            // Primary: accept exact enum names (case-insensitive)
            if (Enum.TryParse<ImportDocumentOptions>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            // Aliases and common misspellings
            switch (normalized.ToLowerInvariant())
            {
                case "override": return ImportDocumentOptions.Override;
                case "none": return ImportDocumentOptions.None;
                case "skipinactiveculture":
                case "skipinactivecultures":
                case "skipinactive":
                case "skipinactivecult":
                    return ImportDocumentOptions.SkipInactiveCultures;
                case "activeinactiveculture":
                case "activateinactivecultures":
                case "activeinactivecultures":
                case "activateinactive":
                    return ImportDocumentOptions.ActivateInactiveCultures;
                default:
                    throw new McpException($"Invalid importOption '{option}'. Allowed: None, Override, SkipInactiveCultures, ActivateInactiveCultures");
            }
        }

        /// <summary>Internal, not private: the type document imports run the same pre-check.</summary>
        internal static List<string> GetResMissingEnUsIds(string directory, string baseName)
        {
            var resPath = Path.Combine(directory, baseName + ".s7res");
            var missing = new List<string>();
            if (!File.Exists(resPath))
            {
                return missing;
            }
            var xdoc = XDocument.Load(resPath);
            XNamespace ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
            foreach (var comment in xdoc.Descendants(ns + "Comment"))
            {
                var hasEnUs = comment.Elements(ns + "MultiLanguageText")
                                     .Any(e => string.Equals((string?)e.Attribute("Lang"), "en-US", StringComparison.OrdinalIgnoreCase));
                if (!hasEnUs)
                {
                    var id = (string?)comment.Attribute("Id") ?? "";
                    missing.Add(id);
                }
            }
            return missing;
        }

        #endregion
    }
}
