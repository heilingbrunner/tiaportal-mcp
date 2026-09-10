using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Blocks.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reading what an object actually contains, rather than only its metadata.
    ///
    /// Callers: the GetBlockSource / GetTypeSource / GetBlockInterface tools in
    /// McpServer.Source.cs. Affected API: none existing - every member here is new.
    ///
    /// File I/O: Openness exposes no in-memory object for a block body - the only way to obtain
    /// the code is to export it - so the source readers export into a scratch directory under
    /// the system temp path and remove it again before returning. Nothing is written to a
    /// caller-visible location, and the project is never modified.
    ///
    /// GetBlockInterface needs no export at all: PlcBlockInterface.Members is available in
    /// memory, and it also works on inconsistent blocks that cannot be exported.
    /// </summary>
    public partial class Portal
    {
        #region scratch directory

        /// <summary>
        /// A throwaway directory for one export. Cleanup is best-effort: leaving a few files in
        /// the temp path is preferable to failing a read that already succeeded.
        /// </summary>
        private sealed class SourceScope : IDisposable
        {
            public SourceScope()
            {
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
                catch (Exception)
                {
                    // Best effort - the OS cleans the temp path eventually.
                }
            }
        }

        #endregion

        #region source text

        /// <summary>
        /// Source text of one program block. 'document' returns the SIMATIC Source Document
        /// (readable SCL/LAD/STL text, TIA Portal V20+); 'xml' returns the SimaticML export,
        /// which is the only option for objects that have no source document.
        /// </summary>
        public SourceTextResult GetBlockSource(string softwarePath, string blockPath, string format = "document", int maxChars = 40000)
        {
            return Operation.Run(_logger, nameof(GetBlockSource), PortalErrorCode.ExportFailed,
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

                    return ReadSource(
                        format,
                        blockPath,
                        block.Name,
                        maxChars,
                        dir => ExportAsDocuments(softwarePath, blockPath, dir),
                        dir => ExportBlock(softwarePath, blockPath, dir) != null);
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("format", format));
        }

        /// <summary>Source text of one PLC data type. Documents require TIA Portal V21.</summary>
        public SourceTextResult GetTypeSource(string softwarePath, string typePath, string format = "document", int maxChars = 40000)
        {
            return Operation.Run(_logger, nameof(GetTypeSource), PortalErrorCode.ExportFailed,
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

                    return ReadSource(
                        format,
                        typePath,
                        type.Name,
                        maxChars,
                        dir => ExportTypeAsDocuments(softwarePath, typePath, dir).Files.Count > 0,
                        dir => ExportType(softwarePath, dir, typePath) != null);
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("format", format));
        }

        /// <summary>
        /// Shared body of the two readers: export into a scratch directory with the requested
        /// exporter, concatenate what landed there, and truncate on a line boundary.
        /// </summary>
        private SourceTextResult ReadSource(
            string format,
            string objectPath,
            string name,
            int maxChars,
            Func<string, bool> exportDocuments,
            Func<string, bool> exportXml)
        {
            var wantsDocument = format.Equals("document", StringComparison.OrdinalIgnoreCase);

            if (!wantsDocument && !format.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Unknown format '{format}'. Use 'document' for readable source text or 'xml' for SimaticML.");
            }

            if (maxChars <= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'maxChars' must be greater than zero.");
            }

            using (var scope = new SourceScope())
            {
                var used = wantsDocument ? "document" : "xml";

                if (wantsDocument)
                {
                    try
                    {
                        exportDocuments(scope.Directory);
                    }
                    catch (PortalException pex) when (IsUnsupportedForDocuments(pex))
                    {
                        // TIA Portal refuses a source document for whole categories of object -
                        // STL blocks and mixed-language blocks among them. XML always exists, so
                        // fall back and say so in Format rather than returning nothing.
                        _logger?.LogWarning("No source document for '{Name}', falling back to XML: {Reason}", name, pex.Message);

                        exportXml(scope.Directory);
                        used = "xml";
                    }
                }
                else
                {
                    exportXml(scope.Directory);
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
                    Format = used,
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

        #region whole-PLC snapshot

        /// <summary>
        /// Writes one PLC software to a folder tree that mirrors the project: program blocks and
        /// PLC data types as SIMATIC Source Documents where TIA Portal supports them, tag tables
        /// and watch tables as XML, each below its localised system folder. The result is a
        /// directory a version control system can diff.
        ///
        /// Composes the existing bulk exporters rather than calling Openness directly, so it
        /// inherits their skip-and-continue behaviour: one object that cannot be exported does
        /// not lose the rest of the snapshot.
        /// </summary>
        public SourceTreeResult ExportPlcAsSourceTree(string softwarePath, string exportPath)
        {
            return Operation.Run(_logger, nameof(ExportPlcAsSourceTree), PortalErrorCode.ExportFailed,
                () =>
                {
                    // Fail before writing anything when the path is wrong.
                    GetPlcSoftwareOrThrow(softwarePath);

                    var result = new SourceTreeResult { Directory = exportPath };

                    // Blocks: documents when the Portal is new enough, XML otherwise, since a
                    // snapshot missing every block would be worse than a less readable one.
                    if (Engineering.TiaMajorVersion >= 20)
                    {
                        var blocks = ExportBlocksAsDocuments(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "blocks", "document", blocks?.Count() ?? 0);
                    }
                    else
                    {
                        var blocks = ExportBlocks(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "blocks", "xml", blocks?.Count() ?? 0);
                    }

                    if (Engineering.TiaMajorVersion >= MinVersionForTypeDocuments)
                    {
                        var types = ExportTypesAsDocuments(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "types", "document", types.Exported.Count);
                        result.Skipped.AddRange(types.Inconsistent.Select(t => $"type {GetTypePath(t)}: inconsistent"));
                        result.Failures.AddRange(types.Failures.Select(f => $"type {f}"));
                    }
                    else
                    {
                        var types = ExportTypes(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "types", "xml", types?.Count() ?? 0);
                    }

                    // Tag and watch tables have no document format in V21; XML is all there is.
                    Record(result, "tagTables", "xml", ExportEach(
                        GetTagTables(softwarePath).Select(t => (GetTagTablePath(t), (Action)(() => ExportTagTable(softwarePath, GetTagTablePath(t), exportPath, preservePath: true)))),
                        "tag table",
                        result));

                    Record(result, "watchTables", "xml", ExportEach(
                        GetWatchTables(softwarePath).Select(t => (GetWatchTablePath(t), (Action)(() => ExportWatchTable(softwarePath, GetWatchTablePath(t), exportPath, preservePath: true)))),
                        "watch table",
                        result));

                    _logger?.LogInformation(
                        "Source tree for '{Software}' written to '{Directory}': {Written} object(s), {Skipped} skipped, {Failed} failed",
                        softwarePath, exportPath, result.TotalWritten, result.Skipped.Count, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath));
        }

        private static void Record(SourceTreeResult result, string area, string format, int count)
        {
            result.Written[area] = count;
            result.Formats[area] = format;
        }

        /// <summary>
        /// Runs one export per object, collecting failures instead of aborting - the table
        /// exporters throw per object, unlike the bulk block and type exporters.
        /// </summary>
        private int ExportEach(IEnumerable<(string Path, Action Export)> items, string kind, SourceTreeResult result)
        {
            var written = 0;

            foreach (var item in items)
            {
                try
                {
                    item.Export();
                    written++;
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{kind} {item.Path}: {ex.Message}");
                    _logger?.LogWarning(ex, "Snapshot could not export {Kind} '{Path}'", kind, item.Path);
                }
            }

            return written;
        }

        #endregion

        #region interface

        /// <summary>
        /// The members of a data block: every entry with its attributes (data type, start value,
        /// comment, retain, accessibility - Openness exposes these as attributes rather than
        /// typed properties, so they are reported as-is and stay correct across versions).
        /// Needs no export and works on inconsistent blocks.
        ///
        /// Data blocks only: V21 Openness puts 'Interface' on DataBlock and offers no equivalent
        /// accessor on CodeBlock, so the VAR_INPUT/VAR_OUTPUT declarations of an FB, FC or OB
        /// have to be read from the declaration part of GetBlockSource instead.
        /// </summary>
        public List<InterfaceMember> GetBlockInterface(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(GetBlockInterface), PortalErrorCode.NotFound,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'ResolveObjectPath' or 'GetBlocks' to find its path.");

                    if (block is not DataBlock dataBlock)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{block.Name}' is a {block.GetType().Name}, and Openness exposes an interface only for data blocks. " +
                            "Use 'GetBlockSource' and read its declaration part instead.");
                    }

                    var members = dataBlock.Interface?.Members
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"Data block '{block.Name}' exposes no members. Know-how protected blocks hide theirs.");

                    return members.Select(ToInterfaceMember).ToList();
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        private static InterfaceMember ToInterfaceMember(Member member)
        {
            var info = new InterfaceMember { Name = member.Name };

            foreach (var attribute in member.GetAttributeInfos())
            {
                try
                {
                    var value = member.GetAttribute(attribute.Name);

                    info.Attributes[attribute.Name] = value?.ToString() ?? string.Empty;
                }
                catch (Exception)
                {
                    // A member can advertise an attribute it cannot currently produce; skipping
                    // one attribute is better than losing the whole signature.
                }
            }

            info.DataTypeName = info.Attributes.TryGetValue("DataTypeName", out var dataType) ? dataType : null;

            return info;
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

    /// <summary>What a whole-PLC snapshot produced.</summary>
    public class SourceTreeResult
    {
        public string Directory { get; set; } = string.Empty;

        /// <summary>Objects written per area: blocks, types, tagTables, watchTables.</summary>
        public Dictionary<string, int> Written { get; set; } = new Dictionary<string, int>();

        /// <summary>Format used per area: 'document' where TIA Portal supports it, else 'xml'.</summary>
        public Dictionary<string, string> Formats { get; set; } = new Dictionary<string, string>();

        /// <summary>Objects deliberately left out, with the reason.</summary>
        public List<string> Skipped { get; set; } = new List<string>();

        /// <summary>Objects that failed to export, with the reason.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        public int TotalWritten => Written.Values.Sum();
    }

    /// <summary>One member of a block interface.</summary>
    public class InterfaceMember
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Convenience copy of the DataTypeName attribute when the member has one.</summary>
        public string? DataTypeName { get; set; }

        /// <summary>Every attribute Openness reports for the member, stringified.</summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }
}
