using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        // Callers: the ExportPlcProgram / ImportPlcProgram tools in McpServer.Plc.cs. Affected API:
        // none existing - the members are new. File I/O: ExportPlcProgram writes a tree below
        // '<exportPath>/<softwarePath>/', ImportPlcProgram reads one.
        //
        // Both formats share one folder layout, the project groups below the localized system
        // folders ('Program blocks/<groups>', 'PLC data types/<groups>'), so an XML file lies next to
        // the source files of its group. The decisions (which format, which role of a file, the
        // retry rounds) live in PlcProgram; this file only talks to TIA Portal.
        //
        // The export reuses GenerateTypes / GenerateBlocks of ExportSources; the import reuses
        // ImportOneSource of ImportSources and ImportBlocksFromXmlFile of ImportXmlBlock.

        #region export

        /// <summary>
        /// Writes every PLC data type and program block of one PLC software: sources where Openness
        /// can generate them, XML for LAD, nothing for the rest. One file per object, always
        /// mirroring the project groups. Skip-and-continue: an object that cannot be written is
        /// reported in Skipped (no usable format, inconsistent, know-how protected) or Failures.
        /// </summary>
        public GeneratedSourcesResult ExportPlcProgram(string softwarePath, string exportPath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(ExportPlcProgram), PortalErrorCode.ExportFailed,
                () =>
                {
                    // Fail before writing anything when the path is wrong.
                    var group = GetSourceSystemGroup(softwarePath);

                    exportPath = ResolveSoftwareRoot(exportPath, softwarePath);

                    var result = new GeneratedSourcesResult { Directory = exportPath };
                    var sourceBlocks = new List<PlcBlock>();
                    var xmlBlocks = new List<PlcBlock>();

                    foreach (var block in GetBlocks(softwarePath, regexName))
                    {
                        var plan = PlcProgram.ClassifyBlock(block is DataBlock, block.ProgrammingLanguage);

                        switch (plan.Format)
                        {
                            case ProgramBlockFormat.Source:
                                sourceBlocks.Add(block);
                                break;

                            case ProgramBlockFormat.Xml:
                                xmlBlocks.Add(block);
                                break;

                            default:
                                result.Skipped.Add($"block {GetBlockPath(block)}: {plan.Reason}");
                                break;
                        }
                    }

                    GenerateTypes(result, group, GetTypes(softwarePath, regexName), exportPath, withDependencies: false, preservePath: true);
                    GenerateBlocks(result, group, sourceBlocks, exportPath, withDependencies: false, preservePath: true);

                    foreach (var block in xmlBlocks)
                    {
                        var blockPath = GetBlockPath(block);

                        if (!Skip(result, "block", blockPath, block.IsConsistent, block.IsKnowHowProtected))
                        {
                            TryGenerate(result, "block", blockPath, () => ExportBlockXml(block, blockPath, exportPath));
                        }
                    }

                    _logger?.LogInformation(
                        "PLC program of '{Software}' written to '{Directory}': {Written} file(s), {Skipped} skipped, {Failed} failed",
                        softwarePath, exportPath, result.Files.Count, result.Skipped.Count, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath), ("regexName", regexName));
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
        /// Imports a tree written by ExportPlcProgram: '.udt' as PLC data types, '.db', '.awl' and
        /// '.scl' as block sources, '.xml' below the block folder as blocks. Each file goes into the
        /// group its folder implies, which must already exist. Existing objects of the same name
        /// are overwritten. Files are imported in rounds (see PlcProgram.RunRounds), so objects
        /// that depend on each other across both formats still come in; what never succeeds is
        /// reported in Failures with the last error.
        /// </summary>
        public PlcProgramImportResult ImportPlcProgram(string softwarePath, string importPath, string regexName = "", bool keepOnError = false)
        {
            return Operation.Run(_logger, nameof(ImportPlcProgram), PortalErrorCode.ImportFailed,
                () =>
                {
                    var software = GetPlcSoftwareOrThrow(softwarePath);

                    importPath = ResolveSoftwareRoot(importPath, softwarePath);

                    if (!Directory.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import directory '{importPath}' does not exist.");
                    }

                    var blockRoot = software.BlockGroup?.Name ?? string.Empty;
                    var typeRoot = software.TypeGroup?.Name ?? string.Empty;
                    var option = keepOnError ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;
                    var result = new PlcProgramImportResult { Directory = importPath };
                    var work = new List<ProgramFile>();

                    foreach (var file in Directory.EnumerateFiles(importPath, "*.*", SearchOption.AllDirectories))
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

                    var rounds = PlcProgram.RunRounds(ordered, file => result.Items.AddRange(ImportProgramFile(softwarePath, file, option)));

                    result.Rounds = rounds.Rounds;
                    result.Failures.AddRange(rounds.Failed.Select(f => $"{f.Item.Display}: {f.Error}"));

                    _logger?.LogInformation(
                        "PLC program imported from '{Directory}' into '{Software}': {Imported} object(s) in {Rounds} round(s), {Failed} failed",
                        importPath, softwarePath, result.Items.Count, result.Rounds, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("importPath", importPath), ("regexName", regexName));
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

    /// <summary>One block or PLC data type imported by ImportPlcProgram.</summary>
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

    /// <summary>What ImportPlcProgram produced.</summary>
    public class PlcProgramImportResult
    {
        public string Directory { get; set; } = string.Empty;

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
