using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
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
        #region source text

        // The six readers mirror the Export tools by kind: Xml (SimaticML), Source (external source
        // files the compiler reads back) and Documents (SIMATIC Source Documents). Each one exports
        // with the matching exporter into a scratch directory and returns the text; none falls back
        // to another kind, so the caller always knows what it got.

        private const string FormatXml = "xml";
        private const string FormatSource = "source";
        private const string FormatDocuments = "documents";

        /// <summary>Block as SimaticML XML. Works for every consistent block.</summary>
        public SourceTextResult GetXmlBlock(string softwarePath, string blockPath, int maxChars = 40000)
        {
            return ReadBlock(nameof(GetXmlBlock), softwarePath, blockPath, FormatXml, maxChars,
                dir => ExportXmlBlock(softwarePath, blockPath, dir) is not null);
        }

        /// <summary>
        /// Block as an external source file ('.scl', '.db' or '.awl'). LAD, FBD and GRAPH blocks have
        /// no source form and are rejected, like <see cref="ExportSourceBlock"/> does.
        /// </summary>
        public SourceTextResult GetSourceBlock(string softwarePath, string blockPath, bool withDependencies = false, int maxChars = 40000)
        {
            return ReadBlock(nameof(GetSourceBlock), softwarePath, blockPath, FormatSource, maxChars,
                dir => ExportSourceBlock(softwarePath, blockPath, dir, withDependencies) is not null);
        }

        /// <summary>
        /// Block as SIMATIC Source Document (readable SCL/LAD text, TIA Portal V20+). STL and
        /// mixed-language blocks have none and are rejected.
        /// </summary>
        public SourceTextResult GetDocumentsBlock(string softwarePath, string blockPath, int maxChars = 40000)
        {
            return ReadBlock(nameof(GetDocumentsBlock), softwarePath, blockPath, FormatDocuments, maxChars,
                dir => ExportAsDocuments(softwarePath, blockPath, dir));
        }

        /// <summary>PLC data type as SimaticML XML.</summary>
        public SourceTextResult GetXmlType(string softwarePath, string typePath, int maxChars = 40000)
        {
            return ReadType(nameof(GetXmlType), softwarePath, typePath, FormatXml, maxChars,
                dir => ExportXmlType(softwarePath, typePath, dir) is not null);
        }

        /// <summary>PLC data type as a '.udt' external source file. Needs no TIA Portal V21.</summary>
        public SourceTextResult GetSourceType(string softwarePath, string typePath, bool withDependencies = false, int maxChars = 40000)
        {
            return ReadType(nameof(GetSourceType), softwarePath, typePath, FormatSource, maxChars,
                dir => ExportSourceType(softwarePath, typePath, dir, withDependencies) is not null);
        }

        /// <summary>PLC data type as SIMATIC Source Document. Requires TIA Portal V21.</summary>
        public SourceTextResult GetDocumentsType(string softwarePath, string typePath, int maxChars = 40000)
        {
            return ReadType(nameof(GetDocumentsType), softwarePath, typePath, FormatDocuments, maxChars,
                dir => ExportTypeAsDocuments(softwarePath, typePath, dir).Files.Count > 0);
        }

        private SourceTextResult ReadBlock(string operation, string softwarePath, string blockPath, string format, int maxChars, Func<string, bool> export)
        {
            return Operation.Run(_logger, operation, PortalErrorCode.ExportFailed,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'ResolveObjectPath' or 'GetBlocks' to find its path.");

                    if (!block.IsConsistent)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState,
                            $"Block '{block.Name}' is inconsistent; TIA Portal cannot export it. Compile the software first, " +
                            "or use 'GetBlockInterface', which works without an export.");
                    }

                    return ReadSource(format, blockPath, block.Name, maxChars, export);
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        private SourceTextResult ReadType(string operation, string softwarePath, string typePath, string format, int maxChars, Func<string, bool> export)
        {
            return Operation.Run(_logger, operation, PortalErrorCode.ExportFailed,
                () =>
                {
                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"PLC data type not found at '{typePath}'. Use 'ResolveObjectPath' or 'GetTypes' to find its path.");

                    if (!type.IsConsistent)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState,
                            $"PLC data type '{type.Name}' is inconsistent; compile the software before reading its source.");
                    }

                    return ReadSource(format, typePath, type.Name, maxChars, export);
                },
                ("softwarePath", softwarePath), ("typePath", typePath));
        }

        #endregion

        // From the former Portal.Source.cs:
        // Reading what an object actually contains, rather than only its metadata.
        //
        // Callers: the GetXml*/GetSource*/GetDocuments* tools and ExportPlcAsDocuments in McpServer.Sources.cs,
        // and the GetBlockInterface tool in McpServer.Blocks.cs.
        // Affected API: none existing - every member here is new. GetBlockInterface itself lives in
        // Portal.Blocks.cs; the source readers, the shared reader, the scratch directory and the whole-PLC export are here.
        //
        // File I/O: Openness exposes no in-memory object for a block body - the only way to obtain
        // the code is to export it - so the readers export into a scratch directory under
        // the system temp path and remove it again before returning. Nothing is written to a
        // caller-visible location, and the project is never modified.
        //
        // GetBlockInterface needs no export at all: PlcBlockInterface.Members is available in
        // memory, and it also works on inconsistent blocks that cannot be exported.

        #region scratch directory

        /// <summary>
        /// A throwaway directory for one export. Cleanup is best-effort: leaving a few files in
        /// the temp path is preferable to failing a read that already succeeded.
        /// </summary>
        private sealed class SourceScope : IDisposable
        {
            private readonly ILogger? _logger;

            public SourceScope(ILogger? logger = null)
            {
                _logger = logger;

                Directory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "TiaMcpServer", Guid.NewGuid().ToString("N"));

                System.IO.Directory.CreateDirectory(Directory);
            }

            public string Directory { get; }

            public void Dispose()
            {
                try
                {
                    if (System.IO.Directory.Exists(Directory))
                    {
                        System.IO.Directory.Delete(Directory, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    // Best effort - the OS cleans the temp path eventually, and a cleanup
                    // failure must never hide the result or exception that actually matters.
                    _logger?.LogWarning(ex, "Could not remove scratch directory '{Directory}'", Directory);
                }
            }
        }

        #endregion

        #region source text

        /// <summary>
        /// Shared body of the six readers: export into a scratch directory with the given
        /// exporter, concatenate what landed there, and truncate on a line boundary.
        /// </summary>
        private SourceTextResult ReadSource(
            string format,
            string objectPath,
            string name,
            int maxChars,
            Func<string, bool> export)
        {
            if (maxChars <= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'maxChars' must be greater than zero.");
            }

            using (var scope = new SourceScope(_logger))
            {
                try
                {
                    export(scope.Directory);
                }
                catch (PortalException pex) when (format == FormatDocuments && IsUnsupportedForDocuments(pex))
                {
                    // TIA Portal refuses a source document for whole categories of object -
                    // STL blocks and mixed-language blocks among them. Say so instead of
                    // silently returning another kind; the XML reader is the alternative.
                    throw new PortalException(PortalErrorCode.NotSupported,
                        $"'{objectPath}' has no source document ({pex.Message}). " +
                        "Use 'GetXmlBlock' / 'GetXmlType' for SimaticML instead.", null, pex);
                }

                var files = System.IO.Directory
                    .GetFiles(scope.Directory, "*.*", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.ExportFailed,
                        $"TIA Portal produced no file for '{objectPath}', so its source cannot be read.");
                }

                var text = string.Join(
                    Environment.NewLine,
                    files.Select(f => files.Count > 1
                        ? $"===== {System.IO.Path.GetFileName(f)} ====={Environment.NewLine}{File.ReadAllText(f)}"
                        : File.ReadAllText(f)));

                var full = text.Length;
                var truncated = full > maxChars;

                if (truncated)
                {
                    // Cut on a line boundary so the caller never sees half a statement.
                    var cut = text.LastIndexOf('\n', Math.Min(maxChars, text.Length - 1));

                    text = text.Substring(0, cut > 0 ? cut : maxChars);
                }

                return new SourceTextResult
                {
                    Name = name,
                    Path = objectPath,
                    Format = format,
                    Text = text,
                    TotalChars = full,
                    Truncated = truncated,
                    FileNames = files.Select(f => System.IO.Path.GetFileName(f)).ToList()
                };
            }
        }

        /// <summary>
        /// Whether a failed document export means "this object can never have one", in which
        /// case falling back to XML is right, as opposed to a real failure worth reporting.
        ///
        /// The check walks the inner exception chain rather than trusting the PortalErrorCode:
        /// the type document exporter maps EngineeringNotSupportedException to NotSupported, but
        /// the older block exporter reports the same condition as ExportFailed, and changing
        /// that would alter what the shipped ExportAsDocuments tool returns.
        /// </summary>
        private static bool IsUnsupportedForDocuments(PortalException exception)
        {
            if (exception.Code == PortalErrorCode.NotSupported)
            {
                return true;
            }

            for (Exception? inner = exception; inner != null; inner = inner.InnerException)
            {
                if (inner is EngineeringNotSupportedException)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region whole-plc snapshot

        #endregion

        // From the former Portal.GenerateSource.cs:
        // Generating TIA Portal external source files (*.scl, *.db, *.awl, *.udt) from blocks and
        // PLC data types.
        //
        // Callers: the ExportSourceBlock / ExportSourceBlocks / ExportSourceType /
        // ExportSourceTypes / ExportSources tools in McpServer.Sources.cs. Affected API: none
        // existing - every member here is new.
        //
        // This is the compiler's own source format, not the SimaticML XML of ExportXmlBlock nor the
        // SIMATIC Source Documents of ExportAsDocuments: the files written here are exactly what
        // 'ImportSourceBlock' / 'ImportSourceType' (or 'ImportExternalSource') reads back, which makes
        // them the round-trippable representation of a program.
        //
        // The rules below come from the Openness manual, section "Generate source from block", and
        // are not negotiable - Openness throws rather than adapting:
        // - only STL and SCL blocks and data blocks can produce a source at all; LAD, FBD, GRAPH
        // and the rest have no textual form and are skipped with a reason,
        // - the file extension must match the object: '.db' for data blocks, '.awl' for STL,
        // '.scl' for SCL, '.udt' for PLC data types,
        // - a file of the same name at the target location is an error, not an overwrite.
        // The last one is smoothed over here: every other exporter in this server overwrites, so
        // the target file is removed first rather than failing a second run.
        //
        // Filesystem only - generating a source does not modify the project, so none of this is
        // gated behind '--allow-write'.

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
            var extension = PlcProgram.SourceExtension(block is DataBlock, block.ProgrammingLanguage);

            return extension != null
                ? (extension, null)
                : (null,
                    $"programming language {block.ProgrammingLanguage} has no source form; " +
                    "Openness generates sources only from STL blocks, SCL blocks and data blocks");
        }

        #endregion

        #region single object

        /// <summary>
        /// Writes one program block as an external source file into '&lt;exportPath&gt;' or, with
        /// <paramref name="preservePath"/>, into '&lt;exportPath&gt;/Program blocks/&lt;groups&gt;'
        /// - the same layout <see cref="ExportXmlBlock"/> writes. The extension follows the block:
        /// '.db', '.awl' or '.scl'.
        /// </summary>
        /// <param name="withDependencies">
        /// Include every object the block uses - called blocks, instance DBs, UDTs - in the same
        /// file, so it compiles on its own. Off by default, which keeps one file per object.
        /// </param>
        public GeneratedSource ExportSourceBlock(string softwarePath, string blockPath, string exportPath, bool withDependencies = false, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportSourceBlock), PortalErrorCode.ExportFailed,
                () =>
                {
                    var group = GetSourceSystemGroup(softwarePath);

                    exportPath = ResolveSoftwareRoot(exportPath, softwarePath);

                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'ResolveObjectPath' or 'GetBlocks' to find its path.");

                    var (extension, reason) = BlockSourceExtension(block);

                    if (extension == null)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"Block '{block.Name}' cannot be generated as a source: {reason}. " +
                            "Use 'ExportXmlBlock' for SimaticML or 'ExportAsDocuments' for a source document instead.");
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
        public GeneratedSource ExportSourceType(string softwarePath, string typePath, string exportPath, bool withDependencies = false, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportSourceType), PortalErrorCode.ExportFailed,
                () =>
                {
                    var group = GetSourceSystemGroup(softwarePath);

                    exportPath = ResolveSoftwareRoot(exportPath, softwarePath);

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

        #region whole plc

        private void GenerateBlocks(
            GeneratedSourcesResult result,
            PlcExternalSourceSystemGroup group,
            IEnumerable<PlcBlock> blocks,
            string exportPath,
            bool withDependencies,
            bool preservePath)
        {
            foreach (var block in blocks)
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
                        BlockSourceDirectory(block, exportPath, preservePath),
                        withDependencies));
            }
        }

        private void GenerateTypes(
            GeneratedSourcesResult result,
            PlcExternalSourceSystemGroup group,
            IEnumerable<PlcType> types,
            string exportPath,
            bool withDependencies,
            bool preservePath)
        {
            foreach (var type in types)
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
                        TypeSourceDirectory(type, exportPath, preservePath),
                        withDependencies));
            }
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

            var file = Path.Combine(directory, FileNames.Safe(name) + extension);

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

        // From the former Portal.ImportSources.cs:
        // Importing TIA Portal external source files (*.scl, *.db, *.awl, *.udt) back into the
        // project as compiled blocks and PLC data types - the counterpart to
        // ExportSourceBlock / ExportSourceType / ExportSources further up in this file.
        //
        // Callers: the ImportSourceBlock / ImportSourceType / ImportSourceBlocks / ImportSourceTypes /
        // ImportSources tools in McpServer.Sources.cs, which mirror the ExportSource* tools one to
        // one. Affected API: none existing - every member here is new, apart from
        // ImportExternalSource, which was ImportSourceBlocks before that name went to the
        // folder import. CreateExternalSourceFromFile, DeleteExternalSource
        // and GetExternalSourcePath (further down in this file) are reused rather
        // than duplicated, and so is StripSystemRootSegment (Portal.Documents.cs), which already
        // solves the same "does this folder name match the localized system root" problem for
        // document imports.
        //
        // Openness has no single "import a source file as a block" call - it is always two steps,
        // per the Openness manual section "Generating blocks from source":
        // 1. register the file as a PlcExternalSource (CreateExternalSourceFromFile),
        // 2. compile it (PlcExternalSource.GenerateBlocksFromSource), which returns a mix of
        // PlcBlock and PlcType objects in one call - the source file's own content decides
        // what comes out, not the caller.
        // Step 1 hands TIA Portal a UTF-8-with-BOM copy of the file, never the file itself, so
        // non-ASCII text survives sources saved without a BOM (SourceFileEncoding).
        // This walks a whole folder tree - typically one ExportSources just wrote - doing both
        // steps per file and deleting the scratch external source afterward, so nothing but the
        // generated blocks and types remains in the project.
        //
        // Group placement mirrors the layout ExportSources writes: a file at
        // '<importPath>/Program blocks/<groups>/<Name>.scl' is generated into block user
        // group '<groups>'. A missing group is reported as a failure for that file rather than
        // created automatically - the group structure is expected to already exist, because these
        // files were themselves generated from blocks/types that lived in it.
        //
        // Project-mutating: registered under '--allow-write' like every other
        // [WriteTool] tool.

        #region whole tree

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
            if (NormalizeGroupPath(groupPath).Length == 0)
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
            if (NormalizeGroupPath(groupPath).Length == 0)
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

        // From the former Portal.ExternalSources.cs:
        // PLC external source files (read side).
        //
        // Callers: the GetExternalSources / GetExternalSourceInfo tools in
        // McpServer.Sources.cs. Affected API: none existing - all members are new.
        // Reads/writes no data files: the source files themselves live inside the project.
        //
        // PlcExternalSource exposes only Name and Parent as typed properties, so anything else a
        // caller needs (file path, timestamps) has to come from the generic attribute bag via
        // Helper.GetAttributeList. No attribute name is hard-coded here for that reason.

        #region external sources (read)

        public PlcExternalSourceSystemGroup? GetExternalSourceRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetExternalSourceRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup,
                ("softwarePath", softwarePath));
        }

        public PlcExternalSourceGroup? GetExternalSourceGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetExternalSourceGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcExternalSourceGroup>(root, groupPath, g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of an external source, e.g. "SourceGroup1/Source_1".</summary>
        public string GetExternalSourcePath(PlcExternalSource source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (source.Parent is PlcExternalSourceGroup group)
            {
                var groupPath = BuildGroupPath<PlcExternalSourceGroup>(
                    group,
                    g => g.Parent as PlcExternalSourceGroup,
                    g => g.Name,
                    g => g is PlcExternalSourceSystemGroup,
                    includeSystemRoot: false);

                return string.IsNullOrEmpty(groupPath) ? source.Name : $"{groupPath}/{source.Name}";
            }

            return source.Name;
        }

        public List<PlcExternalSource> GetExternalSources(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetExternalSources), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcExternalSource>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcExternalSourceGroup, PlcExternalSource>(
                            root, list, g => g.ExternalSources, g => g.Groups, s => s.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcExternalSource? GetExternalSource(string softwarePath, string sourcePath)
        {
            return Operation.Run(_logger, nameof(GetExternalSource), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, sourceName) = SplitPath(sourcePath);

                    return GetExternalSourceGroupByPath(softwarePath, groupPath)?.ExternalSources.Find(sourceName);
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath));
        }

        #endregion

        #region external sources (write)

        public PlcExternalSource CreateExternalSourceFromFile(string softwarePath, string groupPath, string name, string filePath)
        {
            return Operation.Run(_logger, nameof(CreateExternalSourceFromFile), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var group = GetExternalSourceGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source group not found at '{groupPath}'.");

                    if (!File.Exists(filePath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Source file '{filePath}' does not exist on the machine running this server.");
                    }

                    // Never the caller's file itself: TIA Portal reads a source without BOM as
                    // ANSI, which garbles every non-ASCII character. See SourceFileEncoding.
                    // CreateFromFile copies the content into the project, so the scratch copy
                    // can go as soon as it returns.
                    using (var scope = new SourceScope(_logger))
                    {
                        var normalized = SourceFileEncoding.WriteUtf8BomCopy(filePath, scope.Directory);

                        return group.ExternalSources.CreateFromFile(name, normalized);
                    }
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("filePath", filePath));
        }

        public bool DeleteExternalSource(string softwarePath, string sourcePath)
        {
            return Operation.Run(_logger, nameof(DeleteExternalSource), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var source = GetExternalSource(softwarePath, sourcePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source not found at '{sourcePath}'.");

                    source.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath));
        }

        public PlcExternalSourceUserGroup CreateExternalSourceGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateExternalSourceGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetExternalSourceGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteExternalSourceGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteExternalSourceGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var group = GetExternalSourceGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source group not found at '{groupPath}'.");

                    var userGroup = group as PlcExternalSourceUserGroup
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{groupPath}' is the system group 'External source files', which cannot be deleted.");

                    userGroup.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        #endregion
    }

    /// <summary>Source text of one object plus what had to be done to obtain it.</summary>
    public class SourceTextResult
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        /// <summary>'document' or 'xml' - the format actually produced, which may differ from the request.</summary>
        public string Format { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        /// <summary>Length before truncation, so the caller can tell how much was withheld.</summary>
        public int TotalChars { get; set; }

        public bool Truncated { get; set; }

        /// <summary>File names TIA Portal produced, without the scratch directory.</summary>
        public List<string> FileNames { get; set; } = new List<string>();
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

        /// <summary>The 'Language' of a file that is a PLC tag table, so it is neither counted as a block nor as a type.</summary>
        public const string TagTableLanguage = "TagTable";

        /// <summary>The 'Language' of a file that is a PLC watch table.</summary>
        public const string WatchTableLanguage = "WatchTable";

        /// <summary>The 'Language' of a file that is a PLC force table.</summary>
        public const string ForceTableLanguage = "ForceTable";

        private static readonly string[] TableLanguages = { TagTableLanguage, WatchTableLanguage, ForceTableLanguage };

        public string Directory { get; set; } = string.Empty;

        public List<GeneratedSource> Files { get; set; } = new List<GeneratedSource>();

        /// <summary>Objects deliberately left out, with the reason.</summary>
        public List<string> Skipped { get; set; } = new List<string>();

        /// <summary>Objects that failed to generate, with the reason.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        /// <summary>Things the user should know about the run, e.g. a settings file that was created and needs checking.</summary>
        public List<string> Notes { get; set; } = new List<string>();

        /// <summary>Files written per area, so a caller need not count the list itself.</summary>
        public Dictionary<string, int> Written
        {
            get
            {
                var written = new Dictionary<string, int>
                {
                    ["blocks"] = Files.Count(f => !TableLanguages.Contains(f.Language) && f.Format != TypeExtension),
                    ["types"] = Files.Count(f => !TableLanguages.Contains(f.Language) && f.Format == TypeExtension)
                };

                // Only a table export has these areas, so the response of every other export is unchanged.
                foreach (var (language, area) in new[] { (TagTableLanguage, "tagTables"), (WatchTableLanguage, "watchTables"), (ForceTableLanguage, "forceTables") })
                {
                    var count = Files.Count(f => f.Language == language);

                    if (count > 0)
                    {
                        written[area] = count;
                    }
                }

                return written;
            }
        }
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

}
