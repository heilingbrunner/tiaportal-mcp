using Microsoft.Extensions.Logging;
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
    /// Generating TIA Portal external source files (*.scl, *.db, *.awl, *.udt) from blocks and
    /// PLC data types.
    ///
    /// Callers: the GenerateBlockSource / GenerateTypeSource / GenerateSources tools in
    /// McpServer.GenerateSource.cs. Affected API: none existing - every member here is new.
    ///
    /// This is the compiler's own source format, not the SimaticML XML of ExportBlock nor the
    /// SIMATIC Source Documents of ExportAsDocuments: the files written here are exactly what
    /// 'CreateExternalSourceFromFile' plus 'GenerateBlocksFromSource' reads back, which makes
    /// them the round-trippable representation of a program.
    ///
    /// The rules below come from the Openness manual, section "Generate source from block", and
    /// are not negotiable - Openness throws rather than adapting:
    ///   - only STL and SCL blocks and data blocks can produce a source at all; LAD, FBD, GRAPH
    ///     and the rest have no textual form and are skipped with a reason,
    ///   - the file extension must match the object: '.db' for data blocks, '.awl' for STL,
    ///     '.scl' for SCL, '.udt' for PLC data types,
    ///   - a file of the same name at the target location is an error, not an overwrite.
    /// The last one is smoothed over here: every other exporter in this server overwrites, so
    /// the target file is removed first rather than failing a second run.
    ///
    /// Filesystem only - generating a source does not modify the project, so none of this is
    /// gated behind '--allow-write'.
    /// </summary>
    public partial class Portal
    {
        #region extensions

        /// <summary>Extension Openness demands for PLC data types.</summary>
        private const string TypeSourceExtension = ".udt";

        /// <summary>
        /// The extension Openness demands for one block, or a reason why the block has no source
        /// form at all. Data blocks are matched by type rather than by ProgrammingLanguage,
        /// because the DB family spans several language values (DB, CPU_DB, F_DB, Motion_DB)
        /// that all export as '.db'.
        /// </summary>
        private static (string? Extension, string? Reason) BlockSourceExtension(PlcBlock block)
        {
            if (block is DataBlock)
            {
                return (".db", null);
            }

            switch (block.ProgrammingLanguage)
            {
                case ProgrammingLanguage.STL:
                    return (".awl", null);

                case ProgrammingLanguage.SCL:
                    return (".scl", null);

                default:
                    return (null,
                        $"programming language {block.ProgrammingLanguage} has no source form; " +
                        "Openness generates sources only from STL blocks, SCL blocks and data blocks");
            }
        }

        #endregion

        #region single object

        /// <summary>
        /// Writes one program block as an external source file into '&lt;exportPath&gt;' or, with
        /// <paramref name="preservePath"/>, into '&lt;exportPath&gt;/Program blocks/&lt;groups&gt;'
        /// - the same layout <see cref="ExportBlock"/> writes. The extension follows the block:
        /// '.db', '.awl' or '.scl'.
        /// </summary>
        /// <param name="withDependencies">
        /// Include every object the block uses - called blocks, instance DBs, UDTs - in the same
        /// file, so it compiles on its own. Off by default, which keeps one file per object.
        /// </param>
        public GeneratedSource GenerateBlockSource(string softwarePath, string blockPath, string exportPath, bool withDependencies = false, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(GenerateBlockSource), PortalErrorCode.ExportFailed,
                () =>
                {
                    var group = GetSourceSystemGroup(softwarePath);

                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'ResolveObjectPath' or 'GetBlocks' to find its path.");

                    var (extension, reason) = BlockSourceExtension(block);

                    if (extension == null)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"Block '{block.Name}' cannot be generated as a source: {reason}. " +
                            "Use 'ExportBlock' for SimaticML or 'ExportAsDocuments' for a source document instead.");
                    }

                    RequireGeneratable(block.IsConsistent, block.IsKnowHowProtected, "Block", block.Name);

                    return Generate(
                        group,
                        block,
                        block.Name,
                        blockPath,
                        extension,
                        block.ProgrammingLanguage.ToString(),
                        BlockSourceDirectory(block, exportPath, preservePath),
                        withDependencies);
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("exportPath", exportPath));
        }

        /// <summary>
        /// Writes one PLC data type as a '*.udt' external source file, into '&lt;exportPath&gt;'
        /// or, with <paramref name="preservePath"/>, into
        /// '&lt;exportPath&gt;/PLC data types/&lt;groups&gt;'.
        /// </summary>
        public GeneratedSource GenerateTypeSource(string softwarePath, string typePath, string exportPath, bool withDependencies = false, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(GenerateTypeSource), PortalErrorCode.ExportFailed,
                () =>
                {
                    var group = GetSourceSystemGroup(softwarePath);

                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"PLC data type not found at '{typePath}'. Use 'ResolveObjectPath' or 'GetTypes' to find its path.");

                    RequireGeneratable(type.IsConsistent, type.IsKnowHowProtected, "PLC data type", type.Name);

                    return Generate(
                        group,
                        type,
                        type.Name,
                        typePath,
                        TypeSourceExtension,
                        "UDT",
                        TypeSourceDirectory(type, exportPath, preservePath),
                        withDependencies);
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("exportPath", exportPath));
        }

        #endregion

        #region whole PLC

        /// <summary>
        /// Writes every block and PLC data type of one PLC software as external source files into
        /// a folder tree that mirrors the project groups: '&lt;exportPath&gt;/Program blocks/...'
        /// and '&lt;exportPath&gt;/PLC data types/...', one file per object.
        ///
        /// The counterpart to <see cref="ExportPlcAsSourceTree"/>, which snapshots the same tree
        /// as source documents and XML. This one produces the format TIA Portal can compile back
        /// into blocks, at the cost of leaving out everything that has no source form - LAD, FBD
        /// and GRAPH blocks among them.
        ///
        /// Skip-and-continue throughout: one object Openness refuses does not lose the rest, and
        /// every omission is reported with its reason rather than swallowed.
        /// </summary>
        public GeneratedSourcesResult GenerateSources(string softwarePath, string exportPath, string regexName = "", bool withDependencies = false)
        {
            return Operation.Run(_logger, nameof(GenerateSources), PortalErrorCode.ExportFailed,
                () =>
                {
                    // Fail before writing anything when the path is wrong.
                    var group = GetSourceSystemGroup(softwarePath);

                    var result = new GeneratedSourcesResult { Directory = exportPath };

                    foreach (var block in GetBlocks(softwarePath, regexName))
                    {
                        var blockPath = GetBlockPath(block);
                        var (extension, reason) = BlockSourceExtension(block);

                        if (extension == null)
                        {
                            result.Skipped.Add($"block {blockPath}: {reason}");
                            continue;
                        }

                        if (Skip(result, "block", blockPath, block.IsConsistent, block.IsKnowHowProtected))
                        {
                            continue;
                        }

                        TryGenerate(
                            result,
                            "block",
                            blockPath,
                            () => Generate(
                                group,
                                block,
                                block.Name,
                                blockPath,
                                extension,
                                block.ProgrammingLanguage.ToString(),
                                BlockSourceDirectory(block, exportPath, preservePath: true),
                                withDependencies));
                    }

                    foreach (var type in GetTypes(softwarePath, regexName))
                    {
                        var typePath = GetTypePath(type);

                        if (Skip(result, "type", typePath, type.IsConsistent, type.IsKnowHowProtected))
                        {
                            continue;
                        }

                        TryGenerate(
                            result,
                            "type",
                            typePath,
                            () => Generate(
                                group,
                                type,
                                type.Name,
                                typePath,
                                TypeSourceExtension,
                                "UDT",
                                TypeSourceDirectory(type, exportPath, preservePath: true),
                                withDependencies));
                    }

                    _logger?.LogInformation(
                        "Sources for '{Software}' written to '{Directory}': {Written} file(s), {Skipped} skipped, {Failed} failed",
                        softwarePath, exportPath, result.Files.Count, result.Skipped.Count, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath), ("regexName", regexName));
        }

        /// <summary>
        /// Records the two conditions under which Openness never produces a source, and reports
        /// whether the object has to be left out.
        /// </summary>
        private static bool Skip(GeneratedSourcesResult result, string kind, string objectPath, bool isConsistent, bool isKnowHowProtected)
        {
            if (!isConsistent)
            {
                result.Skipped.Add($"{kind} {objectPath}: inconsistent");
                return true;
            }

            if (isKnowHowProtected)
            {
                result.Skipped.Add($"{kind} {objectPath}: know-how protected");
                return true;
            }

            return false;
        }

        private void TryGenerate(GeneratedSourcesResult result, string kind, string objectPath, Func<GeneratedSource> generate)
        {
            try
            {
                result.Files.Add(generate());
            }
            catch (Exception ex)
            {
                result.Failures.Add($"{kind} {objectPath}: {ex.Message}");
                _logger?.LogWarning(ex, "Could not generate a source for {Kind} '{Path}'", kind, objectPath);
            }
        }

        #endregion

        #region shared

        /// <summary>
        /// The generator lives on the external source system group, not on the block, so every
        /// path here needs the PLC software's own group.
        /// </summary>
        private PlcExternalSourceSystemGroup GetSourceSystemGroup(string softwarePath)
        {
            return GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"The software at '{softwarePath}' exposes no external source group, so it cannot generate sources.");
        }

        private static void RequireGeneratable(bool isConsistent, bool isKnowHowProtected, string kind, string name)
        {
            if (!isConsistent)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"{kind} '{name}' is inconsistent; compile the software before generating its source.");
            }

            if (isKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"{kind} '{name}' is know-how protected, and TIA Portal never writes the source of a protected object.");
            }
        }

        /// <summary>
        /// Writes one object and reports what landed on disk. The target file is removed first
        /// because Openness treats an existing file as an error; see the class remarks.
        /// </summary>
        private GeneratedSource Generate(
            PlcExternalSourceSystemGroup group,
            IGenerateSource source,
            string name,
            string objectPath,
            string extension,
            string language,
            string directory,
            bool withDependencies)
        {
            Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, name + extension);

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            group.GenerateSource(
                new[] { source },
                new FileInfo(file),
                withDependencies ? GenerateOptions.WithDependencies : GenerateOptions.None);

            if (!File.Exists(file))
            {
                throw new PortalException(PortalErrorCode.ExportFailed,
                    $"TIA Portal reported no error but wrote no file for '{objectPath}'.");
            }

            _logger?.LogDebug("Generated source '{File}' for '{Path}'", file, objectPath);

            return new GeneratedSource
            {
                Name = name,
                Path = objectPath,
                File = file,
                Format = extension,
                Language = language,
                Size = new FileInfo(file).Length
            };
        }

        /// <summary>Flat, or the project tree below the 'Program blocks' system folder.</summary>
        private string BlockSourceDirectory(PlcBlock block, string exportPath, bool preservePath)
        {
            if (!preservePath || block.Parent is not PlcBlockGroup parentGroup)
            {
                return exportPath;
            }

            var groupPath = GetPlcBlockGroupPath(parentGroup);

            return string.IsNullOrEmpty(groupPath)
                ? exportPath
                : Path.Combine(exportPath, groupPath.Replace('/', '\\'));
        }

        /// <summary>Flat, or the project tree below the 'PLC data types' system folder.</summary>
        private string TypeSourceDirectory(PlcType type, string exportPath, bool preservePath)
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
    }

    /// <summary>One external source file that was written.</summary>
    public class GeneratedSource
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Root-relative path of the object in the project.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Absolute path of the file on this machine.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>The extension Openness demanded: '.db', '.awl', '.scl' or '.udt'.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>Programming language of the block, or 'UDT' for a PLC data type.</summary>
        public string Language { get; set; } = string.Empty;

        public long Size { get; set; }
    }

    /// <summary>What a whole-PLC source generation produced.</summary>
    public class GeneratedSourcesResult
    {
        /// <summary>The extension that marks a file as coming from a PLC data type.</summary>
        private const string TypeExtension = ".udt";

        public string Directory { get; set; } = string.Empty;

        public List<GeneratedSource> Files { get; set; } = new List<GeneratedSource>();

        /// <summary>Objects deliberately left out, with the reason.</summary>
        public List<string> Skipped { get; set; } = new List<string>();

        /// <summary>Objects that failed to generate, with the reason.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        /// <summary>Files written per area, so a caller need not count the list itself.</summary>
        public Dictionary<string, int> Written => new Dictionary<string, int>
        {
            ["blocks"] = Files.Count(f => f.Format != TypeExtension),
            ["types"] = Files.Count(f => f.Format == TypeExtension)
        };
    }
}
