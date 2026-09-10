using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A single orientation call for a PLC software.
    ///
    /// Callers: the GetPlcSummary tool in McpServer.Insight.cs. Affected API: none existing -
    /// every member here is new. Reads and writes no data files.
    ///
    /// Why: answering "what am I looking at?" previously meant GetProjectTree, GetSoftwareTree,
    /// GetBlocks, GetTypes, GetTagTables and GetWatchTables, then counting by hand. This
    /// composes the existing collectors once and reports the numbers plus the things worth
    /// knowing before touching anything: what cannot be exported, and what is locked.
    /// </summary>
    public partial class Portal
    {
        /// <summary>
        /// Counts and health of one PLC software. Every figure comes from the existing
        /// collectors, so it stays consistent with what the individual tools report.
        /// </summary>
        public PlcSummary GetPlcSummary(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetPlcSummary), PortalErrorCode.NotFound,
                () =>
                {
                    var software = GetPlcSoftwareOrThrow(softwarePath);

                    var blocks = GetBlocks(softwarePath);
                    var types = GetTypes(softwarePath);
                    var tagTables = GetTagTables(softwarePath);
                    var watchTables = GetWatchTables(softwarePath);
                    var sources = GetExternalSources(softwarePath);

                    var summary = new PlcSummary
                    {
                        Name = software.Name,
                        SoftwarePath = softwarePath,
                        BlockCount = blocks.Count,
                        TypeCount = types.Count,
                        TagTableCount = tagTables.Count,
                        WatchTableCount = watchTables.Count,
                        ExternalSourceCount = sources.Count,
                        TagCount = GetTags(softwarePath).Count,
                        UserConstantCount = GetUserConstants(softwarePath).Count
                    };

                    foreach (var block in blocks)
                    {
                        Increment(summary.BlocksByKind, block.GetType().Name);
                        Increment(summary.BlocksByLanguage, block.ProgrammingLanguage.ToString());

                        if (!block.IsConsistent)
                        {
                            summary.InconsistentObjects.Add(GetBlockPath(block));
                        }

                        if (block.IsKnowHowProtected)
                        {
                            summary.KnowHowProtectedObjects.Add(GetBlockPath(block));
                        }

                        summary.LastModified = Later(summary.LastModified, SafeDate(() => block.ModifiedDate));
                    }

                    foreach (var type in types)
                    {
                        if (!type.IsConsistent)
                        {
                            summary.InconsistentObjects.Add(GetTypePath(type));
                        }

                        if (type.IsKnowHowProtected)
                        {
                            summary.KnowHowProtectedObjects.Add(GetTypePath(type));
                        }

                        summary.LastModified = Later(summary.LastModified, SafeDate(() => type.ModifiedDate));
                    }

                    return summary;
                },
                ("softwarePath", softwarePath));
        }

        private static void Increment(Dictionary<string, int> counter, string key)
        {
            counter[key] = counter.TryGetValue(key, out var current) ? current + 1 : 1;
        }

        private static DateTime? Later(DateTime? current, DateTime? candidate)
        {
            if (candidate == null)
            {
                return current;
            }

            return current == null || candidate > current ? candidate : current;
        }

        /// <summary>
        /// Openness can throw when an object cannot produce a date; one unreadable date must not
        /// cost the whole summary.
        /// </summary>
        private static DateTime? SafeDate(Func<DateTime> read)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>What a PLC software contains and what is wrong with it, in one object.</summary>
    public class PlcSummary
    {
        public string Name { get; set; } = string.Empty;

        public string SoftwarePath { get; set; } = string.Empty;

        public int BlockCount { get; set; }

        public int TypeCount { get; set; }

        public int TagTableCount { get; set; }

        public int TagCount { get; set; }

        public int UserConstantCount { get; set; }

        public int WatchTableCount { get; set; }

        public int ExternalSourceCount { get; set; }

        /// <summary>Blocks per concrete kind: OB, FB, FC, InstanceDB, GlobalDB.</summary>
        public Dictionary<string, int> BlocksByKind { get; set; } = new Dictionary<string, int>();

        /// <summary>Blocks per programming language: LAD, SCL, STL, FBD, ...</summary>
        public Dictionary<string, int> BlocksByLanguage { get; set; } = new Dictionary<string, int>();

        /// <summary>Objects that will refuse to export until the software is compiled.</summary>
        public List<string> InconsistentObjects { get; set; } = new List<string>();

        /// <summary>Objects whose content is hidden, so source and interface reads will fail.</summary>
        public List<string> KnowHowProtectedObjects { get; set; } = new List<string>();

        /// <summary>Newest modification date across blocks and types, or null when unknown.</summary>
        public DateTime? LastModified { get; set; }
    }
}
