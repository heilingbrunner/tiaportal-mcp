using Siemens.Engineering;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// PLC tag tables, tags and constants (read side).
    ///
    /// Callers: the GetTagTables / GetTagTableInfo / GetTags / GetTagInfo / GetConstants /
    /// ExportTagTable tools in McpServer.cs. Affected API: none existing - all members are new.
    /// ExportTagTable writes one .xml file to the path the caller supplies; nothing else here
    /// touches the file system.
    ///
    /// Path shape: tag table paths are root-relative, e.g. "TagGroup1/Table1"; tag paths append
    /// the tag, e.g. "TagGroup1/Table1/Tag_1". The system root ("PLC tags", localised by TIA
    /// Portal) is not part of a reported path, so every path here round-trips back into these
    /// resolvers. The preservePath export layout does prefix it - the same convention block and
    /// type exports follow - and the resolvers accept it back as an optional leading segment.
    /// </summary>
    public partial class Portal
    {
        #region groups

        public PlcTagTableSystemGroup? GetTagTableRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetTagTableRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).TagTableGroup,
                ("softwarePath", softwarePath));
        }

        public PlcTagTableGroup? GetTagTableGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetTagTableGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).TagTableGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcTagTableGroup>(
                            root, StripTagTableSystemRoot(root, groupPath), g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of a tag table, e.g. "TagGroup1/Table1".</summary>
        /// <param name="includeSystemRoot">
        /// True prefixes the system group name as TIA Portal reports it in the current interface
        /// language (e.g. "PLC tags/TagGroup1/Table1"), which is the layout the preservePath
        /// exports write, matching the block and type exports. False (the default) yields a path
        /// that round-trips back into GetTagTable and GetTagTableGroupByPath.
        /// </param>
        public string GetTagTablePath(PlcTagTable table, bool includeSystemRoot = false)
        {
            if (table == null)
            {
                return string.Empty;
            }

            if (table.Parent is PlcTagTableGroup parentGroup)
            {
                var groupPath = BuildGroupPath<PlcTagTableGroup>(
                    parentGroup,
                    g => g.Parent as PlcTagTableGroup,
                    g => g.Name,
                    g => g is PlcTagTableSystemGroup,
                    includeSystemRoot);

                return string.IsNullOrEmpty(groupPath) ? table.Name : $"{groupPath}/{table.Name}";
            }

            return table.Name;
        }

        /// <summary>
        /// Drops a leading system root segment ("PLC tags", or its translation in the current
        /// TIA Portal interface language) so a group path taken from the export layout resolves
        /// like the root-relative form.
        /// </summary>
        private static string StripTagTableSystemRoot(PlcTagTableSystemGroup root, string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath))
            {
                return groupPath;
            }

            var separator = groupPath.IndexOf('/');
            var head = separator < 0 ? groupPath : groupPath.Substring(0, separator);

            if (!head.Equals(root.Name, StringComparison.OrdinalIgnoreCase))
            {
                return groupPath;
            }

            return separator < 0 ? string.Empty : groupPath.Substring(separator + 1);
        }

        #endregion

        #region tag tables

        public List<PlcTagTable> GetTagTables(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetTagTables), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcTagTable>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).TagTableGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcTagTableGroup, PlcTagTable>(
                            root, list, g => g.TagTables, g => g.Groups, t => t.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcTagTable? GetTagTable(string softwarePath, string tagTablePath)
        {
            return Operation.Run(_logger, nameof(GetTagTable), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, tableName) = SplitPath(tagTablePath);
                    var group = GetTagTableGroupByPath(softwarePath, groupPath);

                    return group?.TagTables.Find(tableName);
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath));
        }

        /// <summary>
        /// Exports one tag table to '&lt;exportPath&gt;/&lt;table&gt;.xml', or, with
        /// <paramref name="preservePath"/>, to
        /// '&lt;exportPath&gt;/PLC tags/&lt;groups&gt;/&lt;table&gt;.xml' - the system group name
        /// as TIA Portal reports it in the current interface language, the same way block and
        /// type exports mirror "Program blocks" and "PLC data types".
        /// Filesystem only - it does not modify the project, so it is not gated behind '--allow-write'.
        /// </summary>
        public PlcTagTable? ExportTagTable(string softwarePath, string tagTablePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportTagTable), PortalErrorCode.ExportFailed,
                () =>
                {
                    var table = GetTagTable(softwarePath, tagTablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table not found at '{tagTablePath}'. Use 'GetTagTables' to list the available tables.");

                    var target = preservePath
                        ? Path.Combine(exportPath, GetTagTablePath(table, includeSystemRoot: true).Replace('/', '\\') + ".xml")
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
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("exportPath", exportPath));
        }

        #endregion

        #region tags and constants

        /// <param name="tagTablePath">Empty searches every tag table of the PLC.</param>
        public List<PlcTag> GetTags(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetTags), PortalErrorCode.NotFound,
                () => SelectFromTables<PlcTag>(softwarePath, tagTablePath, t => t.Tags, t => t.Name, regexName),
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("regexName", regexName));
        }

        /// <param name="tagPath">"Table/Tag" or "Group/Table/Tag" - the table part is required.</param>
        public PlcTag? GetTag(string softwarePath, string tagPath)
        {
            return Operation.Run(_logger, nameof(GetTag), PortalErrorCode.NotFound,
                () =>
                {
                    var (tablePath, tagName) = SplitPath(tagPath);

                    if (string.IsNullOrEmpty(tablePath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"'{tagPath}' does not name a tag table. Use 'TableName/TagName', or 'Group/TableName/TagName'.");
                    }

                    var table = GetTagTable(softwarePath, tablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table not found at '{tablePath}'. Use 'GetTagTables' to list the available tables.");

                    return table.Tags.Find(tagName);
                },
                ("softwarePath", softwarePath), ("tagPath", tagPath));
        }

        public List<PlcUserConstant> GetUserConstants(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetUserConstants), PortalErrorCode.NotFound,
                () => SelectFromTables<PlcUserConstant>(softwarePath, tagTablePath, t => t.UserConstants, c => c.Name, regexName),
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("regexName", regexName));
        }

        public List<PlcSystemConstant> GetSystemConstants(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetSystemConstants), PortalErrorCode.NotFound,
                () => SelectFromTables<PlcSystemConstant>(softwarePath, tagTablePath, t => t.SystemConstants, c => c.Name, regexName),
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("regexName", regexName));
        }

        /// <summary>
        /// Collects one member collection across either a single tag table or every table of the
        /// PLC, applying the optional name regex.
        /// </summary>
        private List<T> SelectFromTables<T>(
            string softwarePath,
            string tagTablePath,
            Func<PlcTagTable, IEnumerable<T>> selector,
            Func<T, string> name,
            string regexName)
        {
            var tables = string.IsNullOrEmpty(tagTablePath)
                ? GetTagTables(softwarePath)
                : new List<PlcTagTable>
                  {
                      GetTagTable(softwarePath, tagTablePath)
                          ?? throw new PortalException(PortalErrorCode.NotFound,
                              $"Tag table not found at '{tagTablePath}'. Use 'GetTagTables' to list the available tables.")
                  };

            var result = new List<T>();

            foreach (var table in tables)
            {
                foreach (var item in selector(table))
                {
                    if (string.IsNullOrEmpty(regexName) || MatchesRegex(name(item), regexName))
                    {
                        result.Add(item);
                    }
                }
            }

            return result;
        }

        /// <summary>An invalid pattern excludes the item rather than failing the whole call.</summary>
        private static bool MatchesRegex(string value, string pattern)
        {
            try
            {
                return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        #endregion
    }
}
