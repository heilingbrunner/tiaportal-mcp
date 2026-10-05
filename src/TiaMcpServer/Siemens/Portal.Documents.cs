using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
// Imported rather than written inline: TiaMcpServer.Siemens.Engineering shadows the
// Siemens.Engineering namespace, so a fully qualified Siemens.Engineering.SW.Types.PlcType
// does not resolve from inside this namespace.
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // From the former Portal.Documents.cs:
        // SIMATIC Source Documents for PLC data types.
        //
        // Callers: the ExportTypeAsDocuments / ExportTypesAsDocuments tools in
        // McpServer.Documents.cs and the ImportTypeFromDocuments / ImportTypesFromDocuments tools in
        // McpServer.Documents.cs. Affected API: none existing - every member here is new; the
        // block document methods (the "block documents" region of this file) are deliberately left untouched.
        //
        // A source document is the human-readable, git-diffable form of an object: '<Name>.s7dcl'
        // carries the declaration and body text, the optional '<Name>.s7res' the comments and
        // language resources. Openness - not this code - decides the file names, so exports report
        // what was actually written and imports discover a document set by base name across
        // DocumentExtensions instead of assuming one literal extension.
        //
        // Path shape: type paths are root-relative ("Common/BtnTyp_X"), the same strings GetTypes
        // returns. With preservePath the export mirrors the project tree below the 'PLC data types'
        // system folder, exactly like the XML ExportXmlType, and the importers accept that folder name
        // back as an optional leading segment of groupPath.
        //
        // Requires TIA Portal V21: PlcType.ExportAsDocuments and PlcTypeComposition.ImportFromDocuments
        // do not exist in older Openness versions (blocks have had their equivalents since V20).

        #region source document files

        /// <summary>The lowest TIA Portal major version that offers documents for PLC data types.</summary>
        private const int MinVersionForTypeDocuments = 21;

        /// <summary>
        /// Extensions that make up a source document set, used to discover a set for import and
        /// to report what an export produced. Adding a format Openness starts to emit is a
        /// one-entry change here.
        /// </summary>
        private static readonly string[] DocumentExtensions =
            { ".s7dcl", ".s7res", ".scl", ".udt", ".db", ".lad", ".fbd", ".stl", ".st" };

        /// <summary>
        /// The subset V21 actually writes, and therefore the only files an export is allowed to
        /// delete beforehand. Deliberately narrower than <see cref="DocumentExtensions"/>: an
        /// export directory may also hold hand-written '.scl' or '.udt' external sources, and
        /// clearing those to make room for an export would destroy the caller's own files.
        /// </summary>
        private static readonly string[] WrittenDocumentExtensions = { ".s7dcl", ".s7res" };

        /// <summary>Central version gate so a minimum version is written once per feature.</summary>
        private static void RequireTiaVersion(int minimum, string operation)
        {
            if (Engineering.TiaMajorVersion < minimum)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{operation}' requires TIA Portal V{minimum} or newer; V{Engineering.TiaMajorVersion} is configured. " +
                    "Start the server with '--tia-major-version 21' when a V21 Portal is installed.");
            }
        }

        private static bool IsDocumentExtension(string path, IEnumerable<string> extensions)
        {
            var extension = Path.GetExtension(path);

            return extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Full paths of the files that currently make up '&lt;baseName&gt;' in a directory.</summary>
        private static List<string> ExistingDocuments(string directory, string baseName, IEnumerable<string> extensions)
        {
            if (!Directory.Exists(directory))
            {
                return new List<string>();
            }

            return Directory
                .GetFiles(directory, $"{baseName}.*", SearchOption.TopDirectoryOnly)
                .Where(f => IsDocumentExtension(f, extensions))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Removes an earlier export of the same object. A file that cannot be deleted is logged
        /// rather than thrown: the export that follows may well overwrite it anyway.
        /// </summary>
        private void DeleteExistingDocuments(string directory, string baseName)
        {
            foreach (var file in ExistingDocuments(directory, baseName, WrittenDocumentExtensions))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not delete existing document {File}", file);
                }
            }
        }

        /// <summary>
        /// Distinct base names of the document sets in a directory, i.e. the values that go into
        /// the 'fileNameWithoutExtension' argument of an import.
        /// </summary>
        private static List<string> FindDocumentBaseNames(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return new List<string>();
            }

            return Directory
                .GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => IsDocumentExtension(f, DocumentExtensions))
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Turns the Openness result into something reportable: the files it says it wrote,
        /// unioned with what is on disk, plus any messages explaining a partial success.
        /// </summary>
        private static DocumentExportInfo Describe(DocumentExportResult? result, string directory, string baseName)
        {
            var files = new List<string>();

            foreach (var reported in ReportedDocuments(result))
            {
                var full = Path.IsPathRooted(reported) ? reported : Path.Combine(directory, reported);

                if (!files.Contains(full, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(full);
                }
            }

            foreach (var found in ExistingDocuments(directory, baseName, DocumentExtensions))
            {
                if (!files.Contains(found, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(found);
                }
            }

            var messages = new List<string>();

            if (result?.Messages != null)
            {
                foreach (var message in result.Messages)
                {
                    if (!string.IsNullOrEmpty(message?.Message))
                    {
                        messages.Add(message.Message);
                    }
                }
            }

            return new DocumentExportInfo
            {
                Name = baseName,
                Directory = directory,
                Files = files,
                Messages = messages,
                State = result == null ? "Failure" : result.State.ToString()
            };
        }

        /// <summary>
        /// Reads DocumentExportResult.ExportedDocuments without depending on its element type:
        /// Openness may hand back plain names, full paths or FileInfo objects, and none of those
        /// should decide whether the caller learns which files exist.
        /// </summary>
        private static IEnumerable<string> ReportedDocuments(DocumentExportResult? result)
        {
            object? exported = result?.ExportedDocuments;

            if (exported is null || exported is string || exported is not System.Collections.IEnumerable items)
            {
                yield break;
            }

            foreach (var item in items)
            {
                var value = item switch
                {
                    null => null,
                    FileInfo file => file.FullName,
                    string text => text,
                    _ => item.ToString()
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value!;
                }
            }
        }

        #endregion

        #region export

        /// <summary>
        /// Exports one PLC data type as a SIMATIC source document set into
        /// '&lt;exportPath&gt;' or, with <paramref name="preservePath"/>, into
        /// '&lt;exportPath&gt;/PLC data types/&lt;groups&gt;'. Filesystem only - it does not modify
        /// the project, so it is not gated behind '--allow-write'.
        /// </summary>
        public DocumentExportInfo ExportTypeAsDocuments(string softwarePath, string typePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportTypeAsDocuments), PortalErrorCode.ExportFailed,
                () =>
                {
                    RequireTiaVersion(MinVersionForTypeDocuments, nameof(ExportTypeAsDocuments));

                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"PLC data type not found at '{typePath}'. Use 'GetTypes' to list the available types.");

                    // TIA Portal never exports inconsistent types
                    if (!type.IsConsistent)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState,
                            $"PLC data type '{type.Name}' is inconsistent; compile the software before exporting it.");
                    }

                    var directory = TypeDocumentDirectory(type, exportPath, preservePath);

                    Directory.CreateDirectory(directory);
                    DeleteExistingDocuments(directory, type.Name);

                    var result = ExportOneType(type, directory);
                    var info = Describe(result, directory, type.Name);

                    if (result == null || result.State == DocumentResultState.Failure)
                    {
                        throw new PortalException(PortalErrorCode.ExportFailed,
                            $"TIA Portal reported '{info.State}' exporting '{type.Name}' as documents. " +
                            (info.Messages.Count > 0 ? string.Join("; ", info.Messages) : "No further detail was returned."));
                    }

                    return info;
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("exportPath", exportPath));
        }

        /// <summary>
        /// Bulk counterpart. A type that cannot be exported does not abort the run: inconsistent
        /// types and per-type failures are collected and returned so the caller can report them.
        /// </summary>
        public TypeDocumentExportResult ExportTypesAsDocuments(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportTypesAsDocuments), PortalErrorCode.ExportFailed,
                () =>
                {
                    RequireTiaVersion(MinVersionForTypeDocuments, nameof(ExportTypesAsDocuments));

                    var types = GetTypes(softwarePath, regexName).ToArray();
                    var outcome = new TypeDocumentExportResult();

                    for (var i = 0; i < types.Length; i++)
                    {
                        var type = types[i];

                        _logger?.LogDebug("- Exporting type {Index}/{Total} as documents: {Name}", i + 1, types.Length, type.Name);

                        if (!type.IsConsistent)
                        {
                            _logger?.LogWarning("Skipping inconsistent type {Name}", type.Name);
                            outcome.Inconsistent.Add(type);

                            continue;
                        }

                        try
                        {
                            var directory = TypeDocumentDirectory(type, exportPath, preservePath);

                            Directory.CreateDirectory(directory);
                            DeleteExistingDocuments(directory, type.Name);

                            var result = ExportOneType(type, directory);
                            var info = Describe(result, directory, type.Name);

                            if (result == null || result.State == DocumentResultState.Failure)
                            {
                                outcome.Failures.Add($"{type.Name}: {info.State}. {string.Join("; ", info.Messages)}".Trim());

                                continue;
                            }

                            outcome.Exported.Add(new TypeDocumentExport { Type = type, Documents = info });
                        }
                        catch (PortalException pex)
                        {
                            outcome.Failures.Add($"{type.Name}: {pex.Message}");
                            _logger?.LogWarning(pex, "Export as documents failed for type {Name}", type.Name);
                        }
                        catch (Exception ex)
                        {
                            outcome.Failures.Add($"{type.Name}: {ex.Message}");
                            _logger?.LogError(ex, "Unexpected error exporting type {Name} as documents", type.Name);
                        }
                    }

                    if (outcome.Failures.Count > 0)
                    {
                        _logger?.LogWarning(
                            "ExportTypesAsDocuments completed with {Failures} failures out of {Total}. First failure: {First}",
                            outcome.Failures.Count, types.Length, outcome.Failures[0]);
                    }
                    else
                    {
                        _logger?.LogInformation("ExportTypesAsDocuments exported {Count} types.", outcome.Exported.Count);
                    }

                    return outcome;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath), ("regexName", regexName));
        }

        /// <summary>
        /// The one place PlcType.ExportAsDocuments is called. EngineeringNotSupportedException is
        /// mapped to NotSupported rather than ExportFailed: the API refuses whole categories of
        /// object (mixed programming languages, and possibly system or library types), and no
        /// caller input would make those succeed.
        /// </summary>
        private static DocumentExportResult? ExportOneType(PlcType type, string directory)
        {
            try
            {
                return type.ExportAsDocuments(new DirectoryInfo(directory), type.Name);
            }
            catch (EngineeringNotSupportedException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"TIA Portal does not offer a source document for '{type.Name}'. {ex.Message}", null, ex);
            }
        }

        /// <summary>
        /// Target directory for one type: flat, or the project tree below the 'PLC data types'
        /// system folder as TIA Portal names it in the current interface language - the layout
        /// the XML ExportXmlType already writes.
        /// </summary>
        private string TypeDocumentDirectory(PlcType type, string exportPath, bool preservePath)
        {
            if (!preservePath || type.Parent is not PlcTypeGroup parentGroup)
            {
                return exportPath;
            }

            var groupPath = GetPlcTypeGroupPath(parentGroup);

            return string.IsNullOrEmpty(groupPath)
                ? exportPath
                : Path.Combine(exportPath, groupPath.Replace('/', '\\'));
        }

        #endregion

        #region import

        /// <summary>
        /// Imports one PLC data type from a source document set. Mutates the project, so the MCP
        /// tool that calls this lives behind '--allow-write'.
        /// </summary>
        /// <param name="groupPath">
        /// Target group below 'PLC data types', empty for the root. A leading system root segment
        /// is accepted so a preservePath export can be fed straight back.
        /// </param>
        public List<PlcType> ImportTypeFromDocuments(
            string softwarePath,
            string groupPath,
            string importPath,
            string fileNameWithoutExtension,
            ImportDocumentOptions option)
        {
            return Operation.Run(_logger, nameof(ImportTypeFromDocuments), PortalErrorCode.ImportFailed,
                () =>
                {
                    RequireTiaVersion(MinVersionForTypeDocuments, nameof(ImportTypeFromDocuments));

                    var group = ResolveTypeGroupForImport(softwarePath, groupPath);

                    if (!Directory.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import directory '{importPath}' does not exist.");
                    }

                    if (ExistingDocuments(importPath, fileNameWithoutExtension, DocumentExtensions).Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"No source document named '{fileNameWithoutExtension}' in '{importPath}'.");
                    }

                    return ImportOneTypeSet(group, importPath, fileNameWithoutExtension, option);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath),
                ("importPath", importPath), ("fileNameWithoutExtension", fileNameWithoutExtension));
        }

        /// <summary>
        /// Imports every source document set in a directory whose base name matches the regex.
        /// A set that fails does not abort the batch, but - unlike the block equivalent - each
        /// failure is logged instead of silently swallowed.
        /// </summary>
        public List<PlcType> ImportTypesFromDocuments(
            string softwarePath,
            string groupPath,
            string importPath,
            string regexName,
            ImportDocumentOptions option)
        {
            return Operation.Run(_logger, nameof(ImportTypesFromDocuments), PortalErrorCode.ImportFailed,
                () =>
                {
                    RequireTiaVersion(MinVersionForTypeDocuments, nameof(ImportTypesFromDocuments));

                    var group = ResolveTypeGroupForImport(softwarePath, groupPath);

                    if (!Directory.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import directory '{importPath}' does not exist.");
                    }

                    var imported = new List<PlcType>();
                    var failures = new List<string>();

                    foreach (var baseName in FindDocumentBaseNames(importPath))
                    {
                        if (!string.IsNullOrEmpty(regexName) && !MatchesRegex(baseName, regexName))
                        {
                            continue;
                        }

                        try
                        {
                            imported.AddRange(ImportOneTypeSet(group, importPath, baseName, option));
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{baseName}: {ex.Message}");
                            _logger?.LogWarning(ex, "Import from documents failed for '{Name}'", baseName);
                        }
                    }

                    if (failures.Count > 0)
                    {
                        _logger?.LogWarning(
                            "ImportTypesFromDocuments completed with {Failures} failures. First failure: {First}",
                            failures.Count, failures[0]);
                    }

                    return imported;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath),
                ("importPath", importPath), ("regexName", regexName));
        }

        private static List<PlcType> ImportOneTypeSet(
            PlcTypeGroup group,
            string importPath,
            string baseName,
            ImportDocumentOptions option)
        {
            DocumentImportResultForTypes? result;

            try
            {
                result = group.Types.ImportFromDocuments(new DirectoryInfo(importPath), baseName, option);
            }
            catch (EngineeringNotSupportedException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"TIA Portal cannot import '{baseName}' from a source document. {ex.Message}", null, ex);
            }

            if (result == null || result.State == DocumentResultState.Failure)
            {
                var detail = DescribeImportMessages(result);

                throw new PortalException(PortalErrorCode.ImportFailed,
                    $"TIA Portal reported '{result?.State.ToString() ?? "Failure"}' importing '{baseName}'. " +
                    (detail.Length > 0 ? detail : "No further detail was returned."));
            }

            return result.ImportedPlcTypes?.Where(t => t != null).ToList() ?? new List<PlcType>();
        }

        private static string DescribeImportMessages(DocumentImportResult? result)
        {
            if (result?.Messages == null)
            {
                return string.Empty;
            }

            return string.Join("; ", result.Messages
                .Where(m => !string.IsNullOrEmpty(m?.Message))
                .Select(m => m.Message));
        }

        /// <summary>
        /// Resolves the target group, accepting a leading 'PLC data types' segment so the folder
        /// layout a preservePath export writes can be handed straight back to an import.
        /// </summary>
        private PlcTypeGroup ResolveTypeGroupForImport(string softwarePath, string groupPath)
        {
            var root = GetPlcSoftwareOrThrow(softwarePath).TypeGroup
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"No PLC data types group found in '{softwarePath}'.");

            var relative = StripSystemRootSegment(root.Name, groupPath);

            if (string.IsNullOrEmpty(relative))
            {
                return root;
            }

            return GetPlcTypeGroupByPath(softwarePath, relative)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"PLC data type group not found at '{groupPath}'. Use 'GetSoftwareTree' to list the available groups.");
        }

        /// <summary>
        /// Drops a leading system root segment, matched against the name TIA Portal reports in
        /// the current interface language rather than a hardcoded English string.
        /// </summary>
        private static string StripSystemRootSegment(string rootName, string path)
        {
            path = NormalizeGroupPath(path);

            if (path.Length == 0)
            {
                return path;
            }

            var separator = path.IndexOf('/');
            var head = separator < 0 ? path : path.Substring(0, separator);

            if (!head.Equals(rootName, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return separator < 0 ? string.Empty : path.Substring(separator + 1);
        }

        #endregion

        #region block documents

        public bool ExportAsDocuments(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block as documents by path: {blockPath}");
            var success = false;
            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "ExportAsDocuments requires TIA Portal V20 or newer");
                }

                
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    if (plcSoftware != null)
                    {
                        // Export code blocks as documents
                        // https://docs.tia.siemens.cloud/r/en-us/v20/creating-and-managing-blocks/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500

                        var groupPath = blockPath.Contains("/") ? blockPath.Substring(0, blockPath.LastIndexOf("/")) : string.Empty;
                        var blockName = blockPath.Contains("/") ? blockPath.Substring(blockPath.LastIndexOf("/") + 1) : blockPath;

                        var group = GetPlcBlockGroupByPath(softwarePath, groupPath);

                        //group?.Blocks.ForEach(b => Console.WriteLine($"Block: {b.Name}, Type: {b.GetType().Name}"));

                        // join exportPath and groupPath
                        if (!Directory.Exists(exportPath))
                        {
                            Directory.CreateDirectory(exportPath);
                        }

                        if (preservePath && !string.IsNullOrEmpty(groupPath))
                        {
                            exportPath = Path.Combine(exportPath, groupPath);

                            if (!Directory.Exists(exportPath))
                            {
                                Directory.CreateDirectory(exportPath);
                            }
                        }

                        try
                        {
                            // delete files s7dcl/s7res if already exists
                            var blockFiles7dclPath = Path.Combine(exportPath, $"{blockName}.s7dcl");
                            if (File.Exists(blockFiles7dclPath))
                            {
                                File.Delete(blockFiles7dclPath);
                            }
                            var blockFiles7resPath = Path.Combine(exportPath, $"{blockName}.s7res");
                            if (File.Exists(blockFiles7resPath))
                            {
                                File.Delete(blockFiles7resPath);
                            }

                            var result = group?.Blocks.Find(blockName)?.ExportAsDocuments(new DirectoryInfo(exportPath), blockName);

                            if (result != null && result.State == DocumentResultState.Success)
                            {
                                success = true;
                            }
                        }
                        catch (EngineeringNotSupportedException ex)
                        {
                            // The export or import of blocks with mixed programming languages is not possible
                            throw new PortalException(PortalErrorCode.ExportFailed, $"EngineeringNotSupportedException at block '{blockName}'. {ex.Message}", null, ex);
                        }
                        catch (Exception ex)
                        {
                            throw new PortalException(PortalErrorCode.ExportFailed, $"Exception at block '{blockName}'. {ex.Message}", null, ex);
                        }

                    }

                }


            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportAsDocuments failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
            return success;
        }

        // TIA portal crashes when exporting blocks as documents, :-(
        public IEnumerable<PlcBlock>? ExportBlocksAsDocuments(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting blocks as documents...");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ExportBlocksAsDocuments is only supported on TIA Portal V20 or newer");
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();

            PlcBlock[] list;
            try
            {
                list = GetBlocks(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to retrieve block list for {softwarePath}");
                return exportList;
            }

            for (int i = 0; i < list.Count(); i++)
            {
                var block = list[i];

                _logger?.LogDebug($"- Exporting block as document {i}/{list.Count()} : {block.Name}");

                // Skip inconsistent blocks (TIA generally won’t export them)
                if (!block.IsConsistent)
                {
                    _logger?.LogWarning($"Skipping inconsistent block {block.Name}");
                    continue;
                }

                // Determine base directory (preserve group path if requested)
                string targetDir = exportPath;
                if (preservePath && block.Parent is PlcBlockGroup parentGroup)
                {
                    var groupPath = GetPlcBlockGroupPath(parentGroup);
                    if (!string.IsNullOrWhiteSpace(groupPath))
                    {
                        targetDir = Path.Combine(exportPath, groupPath.Replace('/', '\\'));
                    }
                }

                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{block.Name}: cannot create directory '{targetDir}' ({ex.Message})");
                    _logger?.LogError(ex, $"Directory creation failed for {targetDir}");
                    continue;
                }

                var fileDcl = Path.Combine(targetDir, $"{block.Name}.s7dcl");
                var fileRes = Path.Combine(targetDir, $"{block.Name}.s7res");

                // Clean previous artifacts
                foreach (var f in new[] { fileDcl, fileRes })
                {
                    try
                    {
                        if (File.Exists(f))
                        {
                            File.Delete(f);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: cannot delete existing '{Path.GetFileName(f)}' ({ex.Message})");
                        _logger?.LogError(ex, $"Failed deleting existing file {f}");
                        // Continue anyway; export might overwrite.
                    }
                }

                try
                {
                    DocumentExportResult? result = null;
                    try
                    {
                        result = block.ExportAsDocuments(new DirectoryInfo(targetDir), block.Name);
                    }
                    catch (EngineeringNotSupportedException ex)
                    {
                        failures.Add($"{block.Name}: not supported ({ex.Message})");
                        _logger?.LogWarning(ex, $"EngineeringNotSupported exporting {block.Name}");
                        continue;
                    }
                    catch (LicenseNotFoundException ex)
                    {
                        failures.Add($"{block.Name}: license not found ({ex.Message})");
                        _logger?.LogError(ex, $"License issue exporting {block.Name}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: export threw ({ex.Message})");
                        _logger?.LogError(ex, $"ExportAsDocuments failed for {block.Name}");
                        continue;
                    }

                    if (result == null)
                    {
                        failures.Add($"{block.Name}: no result returned");
                        continue;
                    }

                    if (result.State == DocumentResultState.Success)
                    {
                        exportList.Add(block);
                    }
                    else
                    {
                        failures.Add($"{block.Name}: result state {result.State}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{block.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, $"Unexpected wrapper error for {block.Name}");
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportBlocksAsDocuments completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
                // Optional verbose list:
                // _logger?.LogDebug("All failures: {Failures}", string.Join("; ", failures));
            }
            else
            {
                _logger?.LogInformation($"ExportBlocksAsDocuments completed successfully. Exported {exportList.Count} blocks.");
            }

            return exportList;
        }

        public bool ImportFromDocuments(string softwarePath, string groupPath, string importPath, string fileNameWithoutExtension, ImportDocumentOptions option)
        {
            _logger?.LogInformation($"Importing block from documents: {fileNameWithoutExtension} in {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportFromDocuments is only supported on TIA Portal V20 or newer");
                return false;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    var dir = new DirectoryInfo(importPath);
                    if (!dir.Exists)
                    {
                        _logger?.LogWarning($"Import directory does not exist: {importPath}");
                        return false;
                    }

                    DocumentImportResult? result = null;
                    try
                    {
                        result = (group is not null)
                            ? group.Blocks.ImportFromDocuments(dir, fileNameWithoutExtension, option)
                            : plcSoftware.BlockGroup.Blocks.ImportFromDocuments(dir, fileNameWithoutExtension, option);
                    }
                    catch (EngineeringNotSupportedException ex)
                    {
                        throw new PortalException(PortalErrorCode.ExportFailed, $"EngineeringNotSupportedException at file '{fileNameWithoutExtension}'. {ex.Message}", null, ex);
                    }

                    if (result is not null && result.State == DocumentResultState.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing block from documents");
            }
            return false;
        }

        public IEnumerable<PlcBlock>? ImportBlocksFromDocuments(string softwarePath, string groupPath, string importPath, string regexName, ImportDocumentOptions option, bool preservePath = false)
        {
            _logger?.LogInformation($"Importing blocks from documents in {importPath} with regex '{regexName}'");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportBlocksFromDocuments is only supported on TIA Portal V20 or newer");
                return null;
            }

            var imported = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    var dir = new DirectoryInfo(importPath);
                    if (!dir.Exists)
                    {
                        _logger?.LogWarning($"Import directory does not exist: {importPath}");
                        return imported;
                    }

                    var rx = string.IsNullOrWhiteSpace(regexName)
                        ? null
                        : new Regex(regexName, RegexOptions.Compiled);

                    // Consider .s7dcl as the primary index; .s7res is optional supplemental
                    var files = dir.GetFiles("*.s7dcl", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file.Name);
                        if (rx != null && !rx.IsMatch(name))
                        {
                            continue;
                        }

                        try
                        {
                            var result = (group is not null)
                                ? group.Blocks.ImportFromDocuments(dir, name, option)
                                : plcSoftware.BlockGroup.Blocks.ImportFromDocuments(dir, name, option);

                            if (result != null && result.State == DocumentResultState.Success && result.ImportedPlcBlocks != null)
                            {
                                foreach (var blk in result.ImportedPlcBlocks)
                                {
                                    if (blk != null)
                                    {
                                        imported.Add(blk);
                                    }
                                }
                            }
                        }
                        catch (EngineeringNotSupportedException)
                        {
                            // mixed languages etc.; skip but continue batch
                        }
                        catch (Exception)
                        {
                            // skip problematic item, continue
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing blocks from documents");
            }

            return imported;
        }

        #endregion
    }

    /// <summary>What one source document export produced, as reported back to the MCP caller.</summary>
    public class DocumentExportInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Directory { get; set; } = string.Empty;

        /// <summary>Full paths of the files that exist after the export.</summary>
        public List<string> Files { get; set; } = new List<string>();

        /// <summary>Openness messages, populated when the state is not a plain success.</summary>
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>DocumentResultState as text: Success, PartialSuccess or Failure.</summary>
        public string State { get; set; } = string.Empty;
    }

    /// <summary>One exported type paired with the files written for it.</summary>
    public class TypeDocumentExport
    {
        public PlcType? Type { get; set; }

        public DocumentExportInfo Documents { get; set; } = new DocumentExportInfo();
    }

    /// <summary>
    /// Outcome of a bulk document export. Unlike ExportBlocksAsDocuments, which only logs its
    /// failures, this hands them back so the tool response can name what did not export.
    /// </summary>
    public class TypeDocumentExportResult
    {
        public List<TypeDocumentExport> Exported { get; set; } = new List<TypeDocumentExport>();

        public List<PlcType> Inconsistent { get; set; } = new List<PlcType>();

        public List<string> Failures { get; set; } = new List<string>();
    }
}
