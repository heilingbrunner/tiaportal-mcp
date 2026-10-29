using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>How one program block travels in ExportPlcProgram / ImportPlcProgram.</summary>
    public enum ProgramBlockFormat
    {
        /// <summary>External source file: '.scl', '.awl' or '.db'.</summary>
        Source,

        /// <summary>SimaticML file: '.xml'. Only LAD blocks, which have no source form.</summary>
        Xml,

        /// <summary>Neither; the block is skipped and the reason is reported.</summary>
        None
    }

    /// <summary>What ImportPlcProgram does with one file of the folder tree. The order is the order of the first import round.</summary>
    public enum ProgramFileKind
    {
        /// <summary>'.udt': a PLC data type source.</summary>
        UdtSource,

        /// <summary>'.db', '.awl', '.scl': a block source.</summary>
        BlockSource,

        /// <summary>'.xml' below the 'Program blocks' folder: a SimaticML block.</summary>
        XmlBlock,

        /// <summary>Not part of the program tree; see the reason.</summary>
        Ignored
    }

    /// <summary>The decision for one block: the format, the file extension and, for None, the reason.</summary>
    public sealed class BlockPlan
    {
        public BlockPlan(ProgramBlockFormat format, string? extension, string? reason)
        {
            Format = format;
            Extension = extension;
            Reason = reason;
        }

        public ProgramBlockFormat Format { get; }

        public string? Extension { get; }

        public string? Reason { get; }
    }

    /// <summary>The decision for one file of an import tree. A null reason means "not part of the tree, say nothing".</summary>
    public sealed class FilePlan
    {
        public FilePlan(ProgramFileKind kind, string? reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public ProgramFileKind Kind { get; }

        public string? Reason { get; }
    }

    /// <summary>The outcome of <see cref="PlcProgram.RunRounds{T}"/>.</summary>
    public sealed class RoundsResult<T>
    {
        /// <summary>Items that were imported, in the order they succeeded.</summary>
        public List<T> Done { get; } = new List<T>();

        /// <summary>Items that never succeeded, each with the error of its last attempt.</summary>
        public List<(T Item, string Error)> Failed { get; } = new List<(T Item, string Error)>();

        /// <summary>How many passes over the pending items were made.</summary>
        public int Rounds { get; set; }
    }

    /// <summary>
    /// The rules behind ExportPlcProgram and ImportPlcProgram, free of TIA Portal so they can be
    /// tested without it:
    /// - which format a block gets: source files where Openness can generate them (SCL, STL,
    ///   data blocks), XML for LAD, nothing for the rest;
    /// - which role a file of the import tree plays;
    /// - the rounds that let objects depend on each other across both formats.
    ///
    /// Callers: Portal.Plc.cs, and Portal.Sources.cs for the extension table. No file I/O.
    /// </summary>
    public static class PlcProgram
    {
        public const string XmlExtension = ".xml";

        /// <summary>
        /// The extension Openness demands for the source of one block, or null when the block has
        /// no source form. Data blocks are matched by type rather than by language, because the DB
        /// family spans several language values that all export as '.db'.
        /// </summary>
        public static string? SourceExtension(bool isDataBlock, ProgrammingLanguage language)
        {
            if (isDataBlock)
            {
                return ".db";
            }

            switch (language)
            {
                case ProgrammingLanguage.STL:
                    return ".awl";

                case ProgrammingLanguage.SCL:
                    return ".scl";

                default:
                    return null;
            }
        }

        /// <summary>Source if Openness can generate one, XML for LAD, otherwise nothing.</summary>
        public static BlockPlan ClassifyBlock(bool isDataBlock, ProgrammingLanguage language)
        {
            var extension = SourceExtension(isDataBlock, language);

            if (extension != null)
            {
                return new BlockPlan(ProgramBlockFormat.Source, extension, null);
            }

            if (language == ProgrammingLanguage.LAD)
            {
                return new BlockPlan(ProgramBlockFormat.Xml, XmlExtension, null);
            }

            return new BlockPlan(
                ProgramBlockFormat.None,
                null,
                $"programming language {language} has no source form and only LAD is exported as XML by this tool; " +
                "use 'ExportXmlBlock' or 'ExportAsDocuments' for it");
        }

        /// <summary>
        /// Decides what a file of the import tree is. <paramref name="relativeDirectory"/> is its
        /// folder below the import root with forward slashes; the first segment is the localized
        /// system folder ('Program blocks', 'PLC data types'). An '.xml' file only counts below the
        /// block folder, because '.xml' also holds tag tables and the like.
        /// </summary>
        public static FilePlan ClassifyFile(string relativeDirectory, string extension, string blockRoot, string typeRoot)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".udt":
                    return new FilePlan(ProgramFileKind.UdtSource, null);

                case ".db":
                case ".awl":
                case ".scl":
                    return new FilePlan(ProgramFileKind.BlockSource, null);

                case XmlExtension:
                    return FirstSegment(relativeDirectory).Equals(blockRoot, StringComparison.OrdinalIgnoreCase) && blockRoot.Length > 0
                        ? new FilePlan(ProgramFileKind.XmlBlock, null)
                        : new FilePlan(ProgramFileKind.Ignored,
                            $"an XML file is imported only below the '{blockRoot}' folder; '{typeRoot}' and other folders hold sources or other kinds of XML");

                case ".stl":
                    return new FilePlan(ProgramFileKind.Ignored,
                        "'.stl' is not a TIA Portal source extension; STL blocks are written as '.awl'");

                default:
                    return new FilePlan(ProgramFileKind.Ignored, null);
            }
        }

        /// <summary>
        /// Imports the items in rounds. An object may need another one that comes later in the list
        /// (a LAD block calls an SCL block, an instance data block needs its function block), so
        /// no fixed order fits every project. Every round tries all pending items; the ones that
        /// fail stay pending and are tried again as long as the last round imported at least one
        /// item. What is left then failed for a reason of its own and is reported with the error
        /// of its last attempt.
        /// </summary>
        /// <param name="items">The work list, in the order of the first round.</param>
        /// <param name="import">Imports one item; throws when it cannot.</param>
        public static RoundsResult<T> RunRounds<T>(IEnumerable<T> items, Action<T> import)
        {
            var result = new RoundsResult<T>();
            var pending = items.Select(item => (Item: item, Error: string.Empty)).ToList();

            while (pending.Count > 0)
            {
                result.Rounds++;

                var failed = new List<(T Item, string Error)>();

                foreach (var (item, _) in pending)
                {
                    try
                    {
                        import(item);
                        result.Done.Add(item);
                    }
                    catch (Exception ex)
                    {
                        failed.Add((item, ex.Message));
                    }
                }

                var progressed = failed.Count < pending.Count;

                pending = failed;

                if (!progressed)
                {
                    break;
                }
            }

            result.Failed.AddRange(pending.Select(p => (p.Item, p.Error)));

            return result;
        }

        private static string FirstSegment(string relativeDirectory)
        {
            var separator = relativeDirectory.IndexOf('/');

            return separator < 0 ? relativeDirectory : relativeDirectory.Substring(0, separator);
        }
    }
}
