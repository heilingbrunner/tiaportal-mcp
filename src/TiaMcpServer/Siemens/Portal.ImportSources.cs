using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Importing TIA Portal external source files (*.scl, *.db, *.awl, *.udt) back into the
    /// project as compiled blocks and PLC data types - the counterpart to
    /// Portal.GenerateSource.cs.
    ///
    /// Callers: the ImportSources tool in McpServerWrite.ImportSources.cs. Affected API: none
    /// existing - every member here is new. CreateExternalSourceFromFile, DeleteExternalSource
    /// and GetExternalSourcePath (Portal.Write.cs / Portal.ExternalSources.cs) are reused rather
    /// than duplicated, and so is StripSystemRootSegment (Portal.Documents.cs), which already
    /// solves the same "does this folder name match the localized system root" problem for
    /// document imports.
    ///
    /// Openness has no single "import a source file as a block" call - it is always two steps,
    /// per the Openness manual section "Generating blocks from source":
    ///   1. register the file as a PlcExternalSource (CreateExternalSourceFromFile),
    ///   2. compile it (PlcExternalSource.GenerateBlocksFromSource), which returns a mix of
    ///      PlcBlock and PlcType objects in one call - the source file's own content decides
    ///      what comes out, not the caller.
    /// This walks a whole folder tree - typically one GenerateSources just wrote - doing both
    /// steps per file and deleting the scratch external source afterward, so nothing but the
    /// generated blocks and types remains in the project.
    ///
    /// Group placement mirrors the layout GenerateSources writes: a file at
    /// '&lt;importPath&gt;/Program blocks/&lt;groups&gt;/&lt;Name&gt;.scl' is generated into block user
    /// group '&lt;groups&gt;'. A missing group is reported as a failure for that file rather than
    /// created automatically - the group structure is expected to already exist, because these
    /// files were themselves generated from blocks/types that lived in it.
    ///
    /// Project-mutating: registered under '--allow-write' like every other tool in
    /// McpServerWrite.
    /// </summary>
    public partial class Portal
    {
        #region whole tree

        private static bool IsTypeSourceExtension(string extension) =>
            extension.Equals(".udt", StringComparison.OrdinalIgnoreCase);

        private static bool IsBlockSourceExtension(string extension) =>
            extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".awl", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".scl", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Walks a folder tree of external source files and generates a block or PLC data type
        /// from each, into the group its folder path implies. The compilable counterpart to
        /// GenerateSources: skip-and-continue throughout, so one bad file does not lose the rest
        /// of the tree.
        /// </summary>
        public ImportedSourcesResult ImportSources(string softwarePath, string importPath, string regexName = "", bool keepOnError = false)
        {
            return Operation.Run(_logger, nameof(ImportSources), PortalErrorCode.ImportFailed,
                () =>
                {
                    var software = GetPlcSoftwareOrThrow(softwarePath);

                    if (!Directory.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import directory '{importPath}' does not exist.");
                    }

                    var blockRootName = software.BlockGroup?.Name ?? string.Empty;
                    var typeRootName = software.TypeGroup?.Name ?? string.Empty;
                    var option = keepOnError ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;

                    var result = new ImportedSourcesResult { Directory = importPath };

                    var files = Directory
                        .EnumerateFiles(importPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => IsBlockSourceExtension(Path.GetExtension(f)) || IsTypeSourceExtension(Path.GetExtension(f)))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                    foreach (var file in files)
                    {
                        var extension = Path.GetExtension(file);
                        var isType = IsTypeSourceExtension(extension);
                        var name = Path.GetFileNameWithoutExtension(file);

                        if (!string.IsNullOrEmpty(regexName) && !MatchesRegex(name, regexName))
                        {
                            continue;
                        }

                        try
                        {
                            var relativeDir = RelativeDirectory(importPath, file);
                            var groupPath = StripSystemRootSegment(isType ? typeRootName : blockRootName, relativeDir);

                            result.Items.AddRange(ImportOneSource(softwarePath, file, groupPath, option, isType));
                        }
                        catch (Exception ex)
                        {
                            result.Failures.Add($"{name}{extension}: {ex.Message}");
                            _logger?.LogWarning(ex, "Could not import source '{File}'", file);
                        }
                    }

                    _logger?.LogInformation(
                        "Sources imported from '{Directory}' into '{Software}': {Imported} object(s), {Failed} failed",
                        importPath, softwarePath, result.Items.Count, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("importPath", importPath), ("regexName", regexName));
        }

        /// <summary>
        /// Registers one file as a scratch external source, compiles it, and removes the scratch
        /// source again regardless of outcome - only the generated blocks/types are meant to
        /// remain in the project, never the intermediate source object.
        /// </summary>
        private List<ImportedSource> ImportOneSource(string softwarePath, string file, string groupPath, GenerateBlockOption option, bool isType)
        {
            // A fresh name every time, unrelated to the file's own name, so this can never
            // collide with an external source the caller already has in the project.
            var scratchName = $"_ImportSources_{Guid.NewGuid():N}";
            var source = CreateExternalSourceFromFile(softwarePath, string.Empty, scratchName, file);

            try
            {
                IList<IEngineeringObject> generated;

                if (isType)
                {
                    var target = ResolveTypeUserGroupForGenerate(softwarePath, groupPath);
                    generated = target == null
                        ? source.GenerateBlocksFromSource(option)
                        : source.GenerateBlocksFromSource(target, option);
                }
                else
                {
                    var target = ResolveBlockUserGroupForGenerate(softwarePath, groupPath);
                    generated = target == null
                        ? source.GenerateBlocksFromSource(option)
                        : source.GenerateBlocksFromSource(target, option);
                }

                var items = new List<ImportedSource>();

                foreach (var item in generated)
                {
                    if (item is PlcBlock block)
                    {
                        items.Add(new ImportedSource { Name = block.Name, Path = GetBlockPath(block), Kind = "block" });
                    }
                    else if (item is PlcType type)
                    {
                        items.Add(new ImportedSource { Name = type.Name, Path = GetTypePath(type), Kind = "type" });
                    }
                }

                return items;
            }
            finally
            {
                try
                {
                    DeleteExternalSource(softwarePath, GetExternalSourcePath(source));
                }
                catch (Exception ex)
                {
                    // Best effort: the scratch source has already done its job. Losing the
                    // exception that actually matters for a cleanup failure would be worse.
                    _logger?.LogWarning(ex, "Could not remove scratch external source for '{File}'", file);
                }
            }
        }

        /// <summary>Empty groupPath means the source's own default location.</summary>
        private PlcBlockUserGroup? ResolveBlockUserGroupForGenerate(string softwarePath, string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath))
            {
                return null;
            }

            var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Block group not found at '{groupPath}'. Create it first with 'CreateBlockGroup'.");

            return group as PlcBlockUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the 'Program blocks' system root; blocks cannot be generated directly into it. " +
                    "Move the file out of the root folder, or leave its group empty to use the source's default location.");
        }

        /// <summary>Empty groupPath means the source's own default location.</summary>
        private PlcTypeUserGroup? ResolveTypeUserGroupForGenerate(string softwarePath, string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath))
            {
                return null;
            }

            var group = GetPlcTypeGroupByPath(softwarePath, groupPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"PLC data type group not found at '{groupPath}'. Create it first with 'CreateTypeGroup'.");

            return group as PlcTypeUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the 'PLC data types' system root; types cannot be generated directly into it. " +
                    "Move the file out of the root folder, or leave its group empty to use the source's default location.");
        }

        /// <summary>Directory portion of 'file', relative to 'root', with forward slashes.</summary>
        private static string RelativeDirectory(string root, string file)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dirFull = (Path.GetDirectoryName(Path.GetFullPath(file)) ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (dirFull.Length <= rootFull.Length || !dirFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return dirFull.Substring(rootFull.Length)
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        #endregion
    }

    /// <summary>One block or PLC data type generated by ImportSources.</summary>
    public class ImportedSource
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Root-relative path of the new object in the project.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>'block' or 'type'.</summary>
        public string Kind { get; set; } = string.Empty;
    }

    /// <summary>What a whole-tree source import produced.</summary>
    public class ImportedSourcesResult
    {
        public string Directory { get; set; } = string.Empty;

        public List<ImportedSource> Items { get; set; } = new List<ImportedSource>();

        /// <summary>Files that could not be imported, with the reason.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        /// <summary>Objects generated per area, so a caller need not count the list itself.</summary>
        public Dictionary<string, int> Written => new Dictionary<string, int>
        {
            ["blocks"] = Items.Count(i => i.Kind == "block"),
            ["types"] = Items.Count(i => i.Kind == "type")
        };
    }
}
