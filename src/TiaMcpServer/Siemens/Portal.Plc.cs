using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Git;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // The whole program of one PLC in one call, in the format that suits each object:
        // - SCL and STL blocks, data blocks and PLC data types as external source files
        //   ('.scl', '.awl', '.db', '.udt'), the format the compiler reads back;
        // - LAD blocks as SimaticML ('.xml'), because Openness generates no source for them.
        // Everything else (FBD, GRAPH, ...) is skipped and reported with its reason.
        //
        // Callers: the ExportPlcProgram / OverwritePlcProgram / ImportPlcProgramChanges tools in
        // McpServer.Plc.cs. Affected API: none existing - the members are new. File I/O:
        // ExportPlcProgram writes a tree below '<exportPath>/<softwarePath>/', the import
        // tools read one; ImportPlcProgramChanges reads only the files git reports as changed.
        //
        // Both formats share one folder layout, the project groups below the localized system
        // folders ('Program blocks/<groups>', 'PLC data types/<groups>'), so an XML file lies next to
        // the source files of its group. The decisions (which format, which role of a file, the
        // retry rounds) live in PlcProgram; this file only talks to TIA Portal.
        //
        // The export reuses GenerateTypes / GenerateBlocks of ExportSources; the import reuses
        // ImportOneSource of ImportSources and ImportBlocksFromXmlFile of ImportXmlBlock.
        //
        // Both import tools replace objects of the same name in the open project, so they
        // preview by default: with dryRun they only classify the files and say, per object,
        // whether it would be created or overwritten. Nothing in the project changes then.

        #region export

        /// <summary>
        /// Writes every PLC data type and program block of one PLC software, each in the format the
        /// export format settings of '&lt;exportPath&gt;/tia.json' configure for its object type:
        /// SIMATIC SD documents, SimaticML or a source file. The settings are required, because
        /// Openness cannot read them; a missing file or entry fails the call before anything is
        /// written. One file per object, always mirroring the project groups. Skip-and-continue: an
        /// object that cannot be written is reported in Skipped (no usable format, inconsistent,
        /// know-how protected) or Failures.
        /// </summary>
        public GeneratedSourcesResult ExportPlcProgram(string softwarePath, string exportPath)
        {
            return Operation.Run(_logger, nameof(ExportPlcProgram), PortalErrorCode.ExportFailed,
                () =>
                {
                    // Fail before writing anything when the path is wrong or the settings are missing.
                    var group = GetSourceSystemGroup(softwarePath);

                    // The settings file sits at the export root, the program below <softwarePath>.
                    var settings = ExportFormatSettings.Load(exportPath);

                    exportPath = ResolveSoftwareRoot(exportPath, softwarePath);

                    var result = new GeneratedSourcesResult { Directory = exportPath };

                    if (settings.Hint != null)
                    {
                        result.Notes.Add(settings.Hint);
                    }

                    var sourceBlocks = new List<PlcBlock>();
                    var xmlBlocks = new List<PlcBlock>();
                    var documentBlocks = new List<PlcBlock>();

                    foreach (var block in GetBlocks(softwarePath))
                    {
                        var plan = PlcProgram.PlanBlock(block is DataBlock, block.ProgrammingLanguage, settings);

                        switch (plan.Format)
                        {
                            case ProgramBlockFormat.Source:
                                sourceBlocks.Add(block);
                                break;

                            case ProgramBlockFormat.Xml:
                                xmlBlocks.Add(block);
                                break;

                            case ProgramBlockFormat.Document:
                                documentBlocks.Add(block);
                                break;

                            default:
                                result.Skipped.Add($"block {GetBlockPath(block)}: {plan.Reason}");
                                break;
                        }
                    }

                    var types = GetTypes(softwarePath);
                    var typeFormat = PlcProgram.PlanType(settings);

                    ExportProgramTypes(result, group, types, typeFormat, exportPath);
                    GenerateBlocks(result, group, sourceBlocks, exportPath, withDependencies: false, preservePath: true);

                    foreach (var block in xmlBlocks)
                    {
                        var blockPath = GetBlockPath(block);

                        if (!Skip(result, "block", blockPath, block.IsConsistent, block.IsKnowHowProtected))
                        {
                            TryGenerate(result, "block", blockPath, () => ExportBlockXml(block, blockPath, exportPath));
                        }
                    }

                    foreach (var block in documentBlocks)
                    {
                        var blockPath = GetBlockPath(block);

                        if (!Skip(result, "block", blockPath, block.IsConsistent, block.IsKnowHowProtected))
                        {
                            TryGenerate(result, "block", blockPath, () => ExportBlockDocument(block, blockPath, exportPath, result.Notes.Add));
                        }
                    }

                    _logger?.LogInformation(
                        "PLC program of '{Software}' written to '{Directory}': {Written} file(s), {Skipped} skipped, {Failed} failed",
                        softwarePath, exportPath, result.Files.Count, result.Skipped.Count, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath));
        }

        /// <summary>The PLC data types in the configured format: source file, SimaticML or SIMATIC SD documents.</summary>
        private void ExportProgramTypes(
            GeneratedSourcesResult result,
            PlcExternalSourceSystemGroup group,
            IEnumerable<PlcType> types,
            ExportFormatKind format,
            string exportPath)
        {
            if (format == ExportFormatKind.Source)
            {
                GenerateTypes(result, group, types, exportPath, withDependencies: false, preservePath: true);

                return;
            }

            foreach (var type in types)
            {
                var typePath = GetTypePath(type);

                if (Skip(result, "type", typePath, type.IsConsistent, type.IsKnowHowProtected))
                {
                    continue;
                }

                TryGenerate(result, "type", typePath, () => format == ExportFormatKind.Xml
                    ? ExportTypeXml(type, typePath, exportPath)
                    : ExportTypeDocument(type, typePath, exportPath, result.Notes.Add));
            }
        }

        /// <summary>Writes one PLC data type as SimaticML into its group folder, replacing an existing file.</summary>
        private GeneratedSource ExportTypeXml(PlcType type, string typePath, string exportPath)
        {
            var directory = TypeSourceDirectory(type, exportPath, preservePath: true);

            Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, FileNames.Safe(type.Name) + PlcProgram.XmlExtension);

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            type.Export(new FileInfo(file), ExportOptions.None);

            return new GeneratedSource
            {
                Name = type.Name,
                Path = typePath,
                File = file,
                Format = PlcProgram.XmlExtension,
                Language = "UDT",
                Size = new FileInfo(file).Length
            };
        }

        /// <summary>Writes one block as SIMATIC SD documents ('.s7dcl', '.s7res'); needs TIA Portal V20.</summary>
        private GeneratedSource ExportBlockDocument(PlcBlock block, string blockPath, string exportPath, Action<string> warn)
        {
            if (Engineering.TiaMajorVersion < 20)
            {
                throw new PortalException(PortalErrorCode.NotSupported, "SIMATIC SD documents of blocks need TIA Portal V20 or newer");
            }

            var directory = BlockSourceDirectory(block, exportPath, preservePath: true);

            return WriteDocuments(directory, block.Name, blockPath, block.ProgrammingLanguage.ToString(),
                name => block.ExportAsDocuments(new DirectoryInfo(directory), name), warn);
        }

        /// <summary>Writes one PLC data type as SIMATIC SD documents ('.s7dcl', '.s7res'); needs TIA Portal V21.</summary>
        private GeneratedSource ExportTypeDocument(PlcType type, string typePath, string exportPath, Action<string> warn)
        {
            if (Engineering.TiaMajorVersion < 21)
            {
                throw new PortalException(PortalErrorCode.NotSupported, "SIMATIC SD documents of PLC data types need TIA Portal V21 or newer");
            }

            var directory = TypeSourceDirectory(type, exportPath, preservePath: true);

            return WriteDocuments(directory, type.Name, typePath, "UDT", name => ExportOneType(type, directory), warn);
        }

        /// <summary>Replaces the document files of one object and reports the '.s7dcl' file that landed on disk.</summary>
        private GeneratedSource WriteDocuments(
            string directory,
            string objectName,
            string objectPath,
            string language,
            Func<string, DocumentExportResult?> export,
            Action<string> warn)
        {
            Directory.CreateDirectory(directory);

            var baseName = FileNames.Safe(objectName);
            var documentFile = Path.Combine(directory, baseName + PlcProgram.DocumentExtension);
            var resourceFile = Path.Combine(directory, baseName + ".s7res");

            foreach (var stale in new[] { documentFile, resourceFile })
            {
                if (File.Exists(stale))
                {
                    File.Delete(stale);
                }
            }

            var outcome = export(baseName);
            var info = Describe(outcome, directory, baseName);

            if (outcome == null || outcome.State == DocumentResultState.Failure || !File.Exists(documentFile))
            {
                throw new PortalException(PortalErrorCode.ExportFailed,
                    $"TIA Portal reported '{(string.IsNullOrEmpty(info.State) ? "no file" : info.State)}' exporting '{objectPath}' as documents. " +
                    (info.Messages.Count > 0
                        ? string.Join("; ", info.Messages)
                        : "No further detail was returned. If TIA Portal shows 'needs preparation for the format of Simatic SD', " +
                          "select the object there and run Tools > Prepare for Simatic SD, then export again."));
            }

            if (outcome.State == DocumentResultState.PartialSuccess)
            {
                warn($"{objectPath}: exported with warnings" + (info.Messages.Count > 0 ? ": " + string.Join("; ", info.Messages) : string.Empty));
            }

            return new GeneratedSource
            {
                Name = objectName,
                Path = objectPath,
                File = documentFile,
                Format = PlcProgram.DocumentExtension,
                Language = language,
                Size = new FileInfo(documentFile).Length
            };
        }

        /// <summary>
        /// Writes one block as SimaticML into its group folder below <paramref name="exportPath"/>,
        /// the same place ExportXmlBlock puts it with preservePath. An existing file is replaced.
        /// </summary>
        private GeneratedSource ExportBlockXml(PlcBlock block, string blockPath, string exportPath)
        {
            var directory = BlockSourceDirectory(block, exportPath, preservePath: true);

            Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, FileNames.Safe(block.Name) + PlcProgram.XmlExtension);

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            block.Export(new FileInfo(file), ExportOptions.None);

            return new GeneratedSource
            {
                Name = block.Name,
                Path = blockPath,
                File = file,
                Format = PlcProgram.XmlExtension,
                Language = block.ProgrammingLanguage.ToString(),
                Size = new FileInfo(file).Length
            };
        }

        #endregion

        #region import

        /// <summary>One file of the import tree and where it goes.</summary>
        private sealed class ProgramFile
        {
            public ProgramFile(string file, string display, ProgramFileKind kind, string groupPath)
            {
                File = file;
                Display = display;
                Kind = kind;
                GroupPath = groupPath;
            }

            public string File { get; }

            public string Display { get; }

            public ProgramFileKind Kind { get; }

            public string GroupPath { get; }
        }

        /// <summary>
        /// Overwrites the program of a PLC with a tree written by ExportPlcProgram: '.udt' as PLC
        /// data types, '.db', '.awl' and '.scl' as block sources, '.xml' below the block folder as
        /// blocks. Each file goes into the group its folder implies, which must already exist.
        /// Existing objects of the same name are replaced. Files are imported in rounds (see
        /// PlcProgram.RunRounds), so objects that depend on each other across both formats still
        /// come in; what never succeeds is reported in Failures with the last error. With
        /// <paramref name="dryRun"/> nothing is imported; Planned lists what would be.
        /// </summary>
        public PlcProgramImportResult OverwritePlcProgram(string softwarePath, string importPath, string regexName = "", bool keepOnError = false, bool dryRun = true)
        {
            return Operation.Run(_logger, nameof(OverwritePlcProgram), PortalErrorCode.ImportFailed,
                () =>
                {
                    var root = ResolveProgramRoot(softwarePath, importPath);

                    return ImportProgramFiles(softwarePath, root,
                        Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories),
                        regexName, keepOnError, dryRun);
                },
                ("softwarePath", softwarePath), ("importPath", importPath), ("regexName", regexName), ("dryRun", dryRun));
        }

        /// <summary>
        /// Like OverwritePlcProgram, but takes only the files below the folder that git reports as
        /// changed since the last commit: modified, staged and untracked ones. Deleted files cannot
        /// be imported and are listed in Deleted; files with an open merge conflict are listed in
        /// Ignored. The folder must lie in a git repository.
        /// </summary>
        public PlcProgramImportResult ImportPlcProgramChanges(string softwarePath, string exportPath, string regexName = "", bool keepOnError = false, bool dryRun = true)
        {
            return Operation.Run(_logger, nameof(ImportPlcProgramChanges), PortalErrorCode.ImportFailed,
                () =>
                {
                    var root = ResolveProgramRoot(softwarePath, exportPath);

                    GitChangeSet changeSet;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        changeSet = GitRepo.GetChangeSet(root);
                    }
                    catch (GitException ex)
                    {
                        _logger?.LogWarning("git failed after {Elapsed} ms for '{Directory}': {Message}", stopwatch.ElapsedMilliseconds, root, ex.Message);

                        throw new PortalException(PortalErrorCode.InvalidParams, ex.Message, null, ex);
                    }

                    var changes = changeSet.Inside;

                    _logger?.LogInformation(
                        "git reported {Inside} changed file(s) below '{Directory}' and {Outside} elsewhere in {Elapsed} ms",
                        changes.Count, root, changeSet.Outside.Count, stopwatch.ElapsedMilliseconds);

                    var importable = changes
                        .Where(c => c.Kind != GitChangeKind.Deleted && c.Kind != GitChangeKind.Unmerged && File.Exists(c.FullPath))
                        .Select(c => c.FullPath)
                        .ToList();

                    var result = ImportProgramFiles(softwarePath, root, importable, regexName, keepOnError, dryRun);

                    result.Changed = changes.Count;
                    result.Deleted.AddRange(changes.Where(c => c.Kind == GitChangeKind.Deleted).Select(c => RelativeFile(root, c.FullPath)));
                    result.Ignored.AddRange(changes
                        .Where(c => c.Kind == GitChangeKind.Unmerged)
                        .Select(c => $"{RelativeFile(root, c.FullPath)}: open merge conflict, resolve it first"));

                    AddOutsideScope(result, changeSet, root);

                    return result;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath), ("regexName", regexName), ("dryRun", dryRun));
        }

        /// <summary>
        /// The folder a program tree lies in: '<paramref name="path"/>/&lt;softwarePath&gt;'. Fails
        /// before anything else when the software is unknown or the folder does not exist.
        /// </summary>
        private string ResolveProgramRoot(string softwarePath, string path)
        {
            GetPlcSoftwareOrThrow(softwarePath);

            var root = ResolveSoftwareRoot(path, softwarePath);

            if (!Directory.Exists(root))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Import directory '{root}' does not exist.");
            }

            return root;
        }

        /// <summary>Most files outside the program folder that are listed by name; the rest is counted.</summary>
        private const int MaxOutsideScopeListed = 20;

        /// <summary>
        /// Says which files git reports as changed elsewhere in the repository, so a file that was
        /// put in the wrong folder shows up in Ignored instead of just missing from the result.
        /// </summary>
        private static void AddOutsideScope(PlcProgramImportResult result, GitChangeSet changeSet, string root)
        {
            foreach (var change in changeSet.Outside.Take(MaxOutsideScopeListed))
            {
                result.Ignored.Add(
                    $"{RelativeFile(changeSet.RepositoryRoot, change.FullPath)}: changed in git but outside '{root}', so it is not part of this PLC software's program tree");
            }

            if (changeSet.Outside.Count > MaxOutsideScopeListed)
            {
                result.Ignored.Add($"... and {changeSet.Outside.Count - MaxOutsideScopeListed} more changed files outside '{root}'");
            }
        }

        private static string RelativeFile(string root, string file)
        {
            return file.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                : file;
        }

        /// <summary>
        /// Classifies the given files of one program tree below <paramref name="root"/> and, unless
        /// <paramref name="dryRun"/>, imports them in rounds. Shared by both import tools; only
        /// the choice of files differs.
        /// </summary>
        private PlcProgramImportResult ImportProgramFiles(string softwarePath, string root, IEnumerable<string> files, string regexName, bool keepOnError, bool dryRun)
        {
            var software = GetPlcSoftwareOrThrow(softwarePath);
            var importPath = root;
            var blockRoot = software.BlockGroup?.Name ?? string.Empty;
            var typeRoot = software.TypeGroup?.Name ?? string.Empty;
            var option = keepOnError ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;
            var result = new PlcProgramImportResult { Directory = importPath, DryRun = dryRun };
            var work = new List<ProgramFile>();

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);

                if (!string.IsNullOrEmpty(regexName) && !MatchesRegex(name, regexName))
                {
                    continue;
                }

                var directory = RelativeDirectory(importPath, file);
                var display = directory.Length == 0 ? Path.GetFileName(file) : directory + "/" + Path.GetFileName(file);
                var plan = PlcProgram.ClassifyFile(directory, Path.GetExtension(file), blockRoot, typeRoot);

                if (plan.Kind == ProgramFileKind.Ignored)
                {
                    if (plan.Reason != null)
                    {
                        result.Ignored.Add($"{display}: {plan.Reason}");
                    }

                    continue;
                }

                var groupPath = StripSystemRootSegment(plan.Kind == ProgramFileKind.UdtSource ? typeRoot : blockRoot, directory);

                work.Add(new ProgramFile(file, display, plan.Kind, groupPath));
            }

            // Types first, then source blocks, then XML; the rounds cover what that order misses.
            var ordered = work
                .OrderBy(w => w.Kind)
                .ThenBy(w => w.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (dryRun)
            {
                // One lookup per group instead of one per file: each lookup walks the group's
                // objects through Openness, which is what made a large preview take minutes.
                var groupNames = new Dictionary<string, HashSet<string>?>(StringComparer.OrdinalIgnoreCase);

                result.Planned.AddRange(ordered.Select(w => PlanProgramFile(softwarePath, w, groupNames)));

                _logger?.LogInformation(
                    "PLC program preview of '{Directory}' for '{Software}': {Planned} file(s) would be imported, {Ignored} ignored",
                    importPath, softwarePath, result.Planned.Count, result.Ignored.Count);

                return result;
            }

            var rounds = PlcProgram.RunRounds(ordered, file => result.Items.AddRange(ImportProgramFile(softwarePath, file, option)));

            result.Rounds = rounds.Rounds;
            result.Failures.AddRange(rounds.Failed.Select(f => $"{f.Item.Display}: {f.Error}"));

            _logger?.LogInformation(
                "PLC program imported from '{Directory}' into '{Software}': {Imported} object(s) in {Rounds} round(s), {Failed} failed",
                importPath, softwarePath, result.Items.Count, result.Rounds, result.Failures.Count);

            return result;
        }

        /// <summary>
        /// What importing one file would do. The object name is taken from the file name, which is
        /// how ExportPlcProgram wrote it; an object that is found in the project is overwritten, one
        /// that is not is created. When the group itself cannot be read (no project, software or
        /// group found, or Openness fails) the answer is 'unknown': a missing object and a failed
        /// lookup must not look the same.
        /// </summary>
        private PlannedProgramItem PlanProgramFile(string softwarePath, ProgramFile file, Dictionary<string, HashSet<string>?> groupNames)
        {
            var name = Path.GetFileNameWithoutExtension(file.File);
            var path = file.GroupPath.Length == 0 ? name : file.GroupPath + "/" + name;
            var isType = file.Kind == ProgramFileKind.UdtSource;
            var key = (isType ? "type:" : "block:") + file.GroupPath;

            if (!groupNames.TryGetValue(key, out var names))
            {
                names = ObjectNamesInGroup(softwarePath, file.GroupPath, isType);
                groupNames[key] = names;
            }

            var action = names == null
                ? PlannedProgramItem.Unknown
                : names.Contains(name) ? PlannedProgramItem.Overwrite : PlannedProgramItem.Create;

            return new PlannedProgramItem
            {
                File = file.Display,
                Name = name,
                Path = path,
                Kind = isType ? "type" : "block",
                Format = file.Kind == ProgramFileKind.XmlBlock ? "xml" : "source",
                Action = action
            };
        }

        /// <summary>
        /// The names of the blocks or PLC data types directly in one group, or null when the group
        /// cannot be read.
        /// </summary>
        private HashSet<string>? ObjectNamesInGroup(string softwarePath, string groupPath, bool isType)
        {
            try
            {
                var names = isType
                    ? GetPlcTypeGroupByPath(softwarePath, groupPath)?.Types.Select(t => t.Name)
                    : GetPlcBlockGroupByPath(softwarePath, groupPath)?.Blocks.Select(b => b.Name);

                return names == null ? null : new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Could not read the {Kind} group '{Group}' of '{Software}'", isType ? "type" : "block", groupPath, softwarePath);

                return null;
            }
        }

        private List<ImportedProgramItem> ImportProgramFile(string softwarePath, ProgramFile file, GenerateBlockOption option)
        {
            if (file.Kind == ProgramFileKind.XmlBlock)
            {
                return ImportBlocksFromXmlFile(softwarePath, file.GroupPath, file.File)
                    .Select(b => new ImportedProgramItem { Name = b.Name, Path = GetBlockPath(b), Kind = "block", Format = "xml" })
                    .ToList();
            }

            return ImportOneSource(softwarePath, file.File, file.GroupPath, option, isType: file.Kind == ProgramFileKind.UdtSource)
                .Select(i => new ImportedProgramItem { Name = i.Name, Path = i.Path, Kind = i.Kind, Format = "source" })
                .ToList();
        }

        #endregion
    }

    /// <summary>One file a dry run would import, and what the import would do with its object.</summary>
    public class PlannedProgramItem
    {
        public const string Create = "create";
        public const string Overwrite = "overwrite";
        public const string Unknown = "unknown";

        /// <summary>The file, relative to the program folder.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>Object name, taken from the file name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Root-relative path the object has or will get in the project.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>'block' or 'type'.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>'source' for an external source file, 'xml' for SimaticML.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>'create' when the project has no such object yet, 'overwrite' when it has, 'unknown' when the lookup failed.</summary>
        public string Action { get; set; } = Create;
    }

    /// <summary>One block or PLC data type imported by OverwritePlcProgram.</summary>
    public class ImportedProgramItem
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Root-relative path of the new object in the project.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>'block' or 'type'.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>'source' for an external source file, 'xml' for SimaticML.</summary>
        public string Format { get; set; } = string.Empty;
    }

    /// <summary>What OverwritePlcProgram / ImportPlcProgramChanges produced.</summary>
    public class PlcProgramImportResult
    {
        public string Directory { get; set; } = string.Empty;

        /// <summary>True when nothing was imported: Planned says what a real run would do.</summary>
        public bool DryRun { get; set; }

        /// <summary>Dry run only: each file that would be imported and whether it creates or overwrites.</summary>
        public List<PlannedProgramItem> Planned { get; set; } = new List<PlannedProgramItem>();

        /// <summary>ImportPlcProgramChanges: files git reports as deleted; they cannot be imported.</summary>
        public List<string> Deleted { get; set; } = new List<string>();

        /// <summary>ImportPlcProgramChanges: how many files git reports as changed, before filtering.</summary>
        public int Changed { get; set; }

        public List<ImportedProgramItem> Items { get; set; } = new List<ImportedProgramItem>();

        /// <summary>Files that never imported, with the error of the last attempt.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        /// <summary>Files that look like part of the tree but are not imported, with the reason.</summary>
        public List<string> Ignored { get; set; } = new List<string>();

        /// <summary>How many passes over the pending files were made.</summary>
        public int Rounds { get; set; }

        /// <summary>Objects imported per area and per format, so a caller need not count the list itself.</summary>
        public Dictionary<string, int> Written => new Dictionary<string, int>
        {
            ["blocks"] = Items.Count(i => i.Kind == "block"),
            ["types"] = Items.Count(i => i.Kind == "type"),
            ["source"] = Items.Count(i => i.Format == "source"),
            ["xml"] = Items.Count(i => i.Format == "xml")
        };
    }
}
