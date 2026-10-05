using Siemens.Engineering;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // From the former Portal.Tags.cs:
        // PLC tag tables, tags and constants (read side).
        //
        // Callers: the GetTagTables / GetTagTableInfo / GetTags / GetTagInfo / GetConstants /
        // ExportXmlTagTable tools in McpServer.cs. Affected API: none existing - all members are new.
        // ExportXmlTagTable writes one .xml file to the path the caller supplies; nothing else here
        // touches the file system.
        //
        // Path shape: tag table paths are root-relative, e.g. "TagGroup1/Table1"; tag paths append
        // the tag, e.g. "TagGroup1/Table1/Tag_1". The system root ("PLC tags", localised by TIA
        // Portal) is not part of a reported path, so every path here round-trips back into these
        // resolvers. The preservePath export layout does prefix it - the same convention block and
        // type exports follow - and the resolvers accept it back as an optional leading segment.

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
            groupPath = NormalizeGroupPath(groupPath);

            if (groupPath.Length == 0)
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
        public PlcTagTable? ExportXmlTagTable(string softwarePath, string tagTablePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportXmlTagTable), PortalErrorCode.ExportFailed,
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
            var tables = NormalizeGroupPath(tagTablePath).Length == 0
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

        // From the former Portal.WatchTables.cs:
        // PLC watch and force tables (read side).
        //
        // Callers: the GetWatchTables / GetWatchTableInfo / GetForceTables / ExportXmlWatchTable tools
        // in McpServer.Tags.cs. Affected API: none existing - all members are new.
        // ExportXmlWatchTable writes one .xml to the caller-supplied path; nothing else does file I/O.
        //
        // Openness asymmetry to be aware of: watch tables can be created and deleted, force tables
        // cannot - PlcForceTableComposition has no Create and PlcForceTable has no Delete, because
        // the force table is system-owned (one per PLC). Only reads live here either way.

        #region watch tables

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
        public PlcWatchTable? ExportXmlWatchTable(string softwarePath, string watchTablePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportXmlWatchTable), PortalErrorCode.ExportFailed,
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

        #endregion

        // From the former Portal.Write.cs:
        // Create, update and delete for tag tables, tags, user constants, watch tables and external
        // sources. Block and type CRUD lives in Portal.Blocks.cs and Portal.Types.cs.
        // The external source create/delete members live in Portal.Sources.cs.
        //
        // Callers: the corresponding tools in McpServer.Tags.cs, registered only under
        // '--allow-write'. Affected API: none existing - all members are new. File reads: the
        // import methods and CreateExternalSourceFromFile read a caller-supplied file; nothing here
        // writes files. Project changes stay in memory until SaveProject / SaveSession.
        //
        // Openness constraints reflected here: system constants and force tables are read-only
        // (no Create/Delete exists for them), and the default tag table cannot be deleted.

        #region tag tables

        public PlcTagTable CreateTagTable(string softwarePath, string groupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTagTable), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var group = GetTagTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{groupPath}'.");

                    return group.TagTables.Create(name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name));
        }

        public bool DeleteTagTable(string softwarePath, string tagTablePath)
        {
            return Operation.Run(_logger, nameof(DeleteTagTable), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var table = RequireTagTable(softwarePath, tagTablePath);

                    if (table.IsDefault)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{tagTablePath}' is the default tag table and cannot be deleted.");
                    }

                    table.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath));
        }

        public bool RenameTagTable(string softwarePath, string tagTablePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameTagTable), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);
                    RequireTagTable(softwarePath, tagTablePath).Name = newName;
                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("newName", newName));
        }

        public PlcTagTableUserGroup CreateTagTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTagTableGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetTagTableGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteTagTableGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteTagTableGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var group = GetTagTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{groupPath}'.");

                    var userGroup = group as PlcTagTableUserGroup
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{groupPath}' is the system group 'PLC tags', which cannot be deleted.");

                    userGroup.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        public bool ImportXmlTagTable(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportXmlTagTable), PortalErrorCode.ImportFailed,
                () =>
                {
                    var group = GetTagTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{groupPath}'.");

                    if (!File.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import file '{importPath}' does not exist on the machine running this server.");
                    }

                    group.TagTables.Import(
                        new FileInfo(importPath),
                        overwrite ? ImportOptions.Override : ImportOptions.None);

                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("importPath", importPath));
        }

        #endregion

        #region tags and user constants

        public PlcTag CreateTag(string softwarePath, string tagTablePath, string name, string dataTypeName, string logicalAddress)
        {
            return Operation.Run(_logger, nameof(CreateTag), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var table = RequireTagTable(softwarePath, tagTablePath);

                    return string.IsNullOrWhiteSpace(dataTypeName)
                        ? table.Tags.Create(name)
                        : table.Tags.Create(name, dataTypeName, logicalAddress);
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name),
                ("dataTypeName", dataTypeName), ("logicalAddress", logicalAddress));
        }

        /// <summary>Every optional argument left null keeps the tag's current value.</summary>
        public bool UpdateTag(
            string softwarePath,
            string tagPath,
            string? newName = null,
            string? dataTypeName = null,
            string? logicalAddress = null,
            bool? externalAccessible = null,
            bool? externalVisible = null,
            bool? externalWritable = null)
        {
            return Operation.Run(_logger, nameof(UpdateTag), PortalErrorCode.RenameFailed,
                () =>
                {
                    var tag = GetTag(softwarePath, tagPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag not found at '{tagPath}'. Use 'GetTags' to list the available tags.");

                    // Applied before the rename so a later lookup by the new name is not needed.
                    if (dataTypeName != null) tag.DataTypeName = dataTypeName;
                    if (logicalAddress != null) tag.LogicalAddress = logicalAddress;
                    if (externalAccessible.HasValue) tag.ExternalAccessible = externalAccessible.Value;
                    if (externalVisible.HasValue) tag.ExternalVisible = externalVisible.Value;
                    if (externalWritable.HasValue) tag.ExternalWritable = externalWritable.Value;

                    if (newName != null)
                    {
                        EnsureValidName(newName);
                        tag.Name = newName;
                    }

                    return true;
                },
                ("softwarePath", softwarePath), ("tagPath", tagPath));
        }

        public bool DeleteTag(string softwarePath, string tagPath)
        {
            return Operation.Run(_logger, nameof(DeleteTag), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var tag = GetTag(softwarePath, tagPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag not found at '{tagPath}'. Use 'GetTags' to list the available tags.");

                    tag.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("tagPath", tagPath));
        }

        public PlcUserConstant CreateUserConstant(string softwarePath, string tagTablePath, string name, string dataTypeName, string value)
        {
            return Operation.Run(_logger, nameof(CreateUserConstant), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var table = RequireTagTable(softwarePath, tagTablePath);

                    return string.IsNullOrWhiteSpace(dataTypeName)
                        ? table.UserConstants.Create(name)
                        : table.UserConstants.Create(name, dataTypeName, value);
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name));
        }

        /// <summary>Every optional argument left null keeps the constant's current value.</summary>
        public bool UpdateUserConstant(
            string softwarePath,
            string tagTablePath,
            string name,
            string? newName = null,
            string? dataTypeName = null,
            string? value = null)
        {
            return Operation.Run(_logger, nameof(UpdateUserConstant), PortalErrorCode.RenameFailed,
                () =>
                {
                    var constant = RequireUserConstant(softwarePath, tagTablePath, name);

                    if (dataTypeName != null) constant.DataTypeName = dataTypeName;
                    if (value != null) constant.Value = value;

                    if (newName != null)
                    {
                        EnsureValidName(newName);
                        constant.Name = newName;
                    }

                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name));
        }

        public bool DeleteUserConstant(string softwarePath, string tagTablePath, string name)
        {
            return Operation.Run(_logger, nameof(DeleteUserConstant), PortalErrorCode.DeleteFailed,
                () =>
                {
                    RequireUserConstant(softwarePath, tagTablePath, name).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name));
        }

        #endregion

        #region watch tables

        public PlcWatchTable CreateWatchTable(string softwarePath, string groupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateWatchTable), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var group = GetWatchTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{groupPath}'.");

                    return group.WatchTables.Create(name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name));
        }

        public bool RenameWatchTable(string softwarePath, string watchTablePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameWatchTable), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);
                    RequireWatchTable(softwarePath, watchTablePath).Name = newName;
                    return true;
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath), ("newName", newName));
        }

        public bool DeleteWatchTable(string softwarePath, string watchTablePath)
        {
            return Operation.Run(_logger, nameof(DeleteWatchTable), PortalErrorCode.DeleteFailed,
                () =>
                {
                    RequireWatchTable(softwarePath, watchTablePath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath));
        }

        public PlcWatchAndForceTableUserGroup CreateWatchTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateWatchTableGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetWatchTableGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteWatchTableGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteWatchTableGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var group = GetWatchTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{groupPath}'.");

                    var userGroup = group as PlcWatchAndForceTableUserGroup
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{groupPath}' is the system group 'Watch and force tables', which cannot be deleted.");

                    userGroup.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        public bool ImportWatchTable(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportWatchTable), PortalErrorCode.ImportFailed,
                () =>
                {
                    var group = GetWatchTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{groupPath}'.");

                    if (!File.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import file '{importPath}' does not exist on the machine running this server.");
                    }

                    group.WatchTables.Import(
                        new FileInfo(importPath),
                        overwrite ? ImportOptions.Override : ImportOptions.None);

                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("importPath", importPath));
        }

        #endregion

        #region lookup guards

        private PlcTagTable RequireTagTable(string softwarePath, string tagTablePath)
        {
            return GetTagTable(softwarePath, tagTablePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Tag table not found at '{tagTablePath}'. Use 'GetTagTables' to list the available tables.");
        }

        private PlcWatchTable RequireWatchTable(string softwarePath, string watchTablePath)
        {
            return GetWatchTable(softwarePath, watchTablePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Watch table not found at '{watchTablePath}'. Use 'GetWatchTables' to list the available tables.");
        }

        private PlcUserConstant RequireUserConstant(string softwarePath, string tagTablePath, string name)
        {
            return RequireTagTable(softwarePath, tagTablePath).UserConstants.Find(name)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"User constant '{name}' not found in tag table '{tagTablePath}'. " +
                    "System constants are read-only and cannot be modified through Openness.");
        }

        #endregion
    }
}
