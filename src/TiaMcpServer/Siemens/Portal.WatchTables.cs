using Siemens.Engineering;
using Siemens.Engineering.SW.WatchAndForceTables;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// PLC watch and force tables (read side).
    ///
    /// Callers: the GetWatchTables / GetWatchTableInfo / GetForceTables / ExportWatchTable tools
    /// in McpServer.WatchTables.cs. Affected API: none existing - all members are new.
    /// ExportWatchTable writes one .xml to the caller-supplied path; nothing else does file I/O.
    ///
    /// Openness asymmetry to be aware of: watch tables can be created and deleted, force tables
    /// cannot - PlcForceTableComposition has no Create and PlcForceTable has no Delete, because
    /// the force table is system-owned (one per PLC). Only reads live here either way.
    /// </summary>
    public partial class Portal
    {
        public PlcWatchAndForceTableSystemGroup? GetWatchTableRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetWatchTableRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup,
                ("softwarePath", softwarePath));
        }

        public PlcWatchAndForceTableGroup? GetWatchTableGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetWatchTableGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcWatchAndForceTableGroup>(root, groupPath, g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of a watch table, e.g. "WatchGroup1/WatchTable_1".</summary>
        public string GetWatchTablePath(PlcWatchTable table)
        {
            return table == null ? string.Empty : BuildTablePath(table.Parent, table.Name);
        }

        /// <summary>Root-relative path of a force table.</summary>
        public string GetForceTablePath(PlcForceTable table)
        {
            return table == null ? string.Empty : BuildTablePath(table.Parent, table.Name);
        }

        private static string BuildTablePath(IEngineeringObject? parent, string name)
        {
            if (!(parent is PlcWatchAndForceTableGroup group))
            {
                return name;
            }

            var groupPath = BuildGroupPath<PlcWatchAndForceTableGroup>(
                group,
                g => g.Parent as PlcWatchAndForceTableGroup,
                g => g.Name,
                g => g is PlcWatchAndForceTableSystemGroup,
                includeSystemRoot: false);

            return string.IsNullOrEmpty(groupPath) ? name : $"{groupPath}/{name}";
        }

        public List<PlcWatchTable> GetWatchTables(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetWatchTables), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcWatchTable>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcWatchAndForceTableGroup, PlcWatchTable>(
                            root, list, g => g.WatchTables, g => g.Groups, t => t.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcWatchTable? GetWatchTable(string softwarePath, string watchTablePath)
        {
            return Operation.Run(_logger, nameof(GetWatchTable), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, tableName) = SplitPath(watchTablePath);

                    return GetWatchTableGroupByPath(softwarePath, groupPath)?.WatchTables.Find(tableName);
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath));
        }

        public List<PlcForceTable> GetForceTables(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetForceTables), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcForceTable>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcWatchAndForceTableGroup, PlcForceTable>(
                            root, list, g => g.ForceTables, g => g.Groups, t => t.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcForceTable? GetForceTable(string softwarePath, string forceTablePath)
        {
            return Operation.Run(_logger, nameof(GetForceTable), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, tableName) = SplitPath(forceTablePath);

                    return GetWatchTableGroupByPath(softwarePath, groupPath)?.ForceTables.Find(tableName);
                },
                ("softwarePath", softwarePath), ("forceTablePath", forceTablePath));
        }

        /// <summary>
        /// Exports one watch table to '&lt;exportPath&gt;/&lt;table&gt;.xml'. Filesystem only, so
        /// it is not gated behind '--allow-write'.
        /// </summary>
        public PlcWatchTable? ExportWatchTable(string softwarePath, string watchTablePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportWatchTable), PortalErrorCode.ExportFailed,
                () =>
                {
                    var table = GetWatchTable(softwarePath, watchTablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table not found at '{watchTablePath}'. Use 'GetWatchTables' to list the available tables.");

                    var target = preservePath
                        ? Path.Combine(exportPath, GetWatchTablePath(table).Replace('/', '\\') + ".xml")
                        : Path.Combine(exportPath, $"{table.Name}.xml");

                    var directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    table.Export(new FileInfo(target), ExportOptions.None);

                    return table;
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath), ("exportPath", exportPath));
        }
    }
}
