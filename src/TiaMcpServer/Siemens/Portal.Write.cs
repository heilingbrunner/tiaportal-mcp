using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
// Imported rather than written inline: TiaMcpServer.Siemens.Engineering shadows the
// Siemens.Engineering namespace, so a fully qualified Siemens.Engineering.SW.Types.PlcType
// does not resolve from inside this namespace.
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.SW.WatchAndForceTables;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Create, update and delete for tag tables, tags, user constants, watch tables and external
    /// sources. Block and type CRUD lives in Portal.BlockCrud.cs.
    ///
    /// Callers: the corresponding tools in McpServerWrite.cs, registered only under
    /// '--allow-write'. Affected API: none existing - all members are new. File reads: the
    /// import methods and CreateExternalSourceFromFile read a caller-supplied file; nothing here
    /// writes files. Project changes stay in memory until SaveProject / SaveSession.
    ///
    /// Openness constraints reflected here: system constants and force tables are read-only
    /// (no Create/Delete exists for them), and the default tag table cannot be deleted.
    /// </summary>
    public partial class Portal
    {
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

        public bool ImportTagTable(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportTagTable), PortalErrorCode.ImportFailed,
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

        #region external sources

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

                    return group.ExternalSources.CreateFromFile(name, filePath);
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

        /// <summary>
        /// Compiles an external source into blocks. An empty target generates into the source's
        /// default location; a target must resolve to a block USER group - the system root does
        /// not bind to the PlcBlockUserGroup overload.
        /// </summary>
        public List<string> GenerateBlocksFromSource(string softwarePath, string sourcePath, string targetGroupPath = "", bool keepOnError = false)
        {
            return Operation.Run(_logger, nameof(GenerateBlocksFromSource), PortalErrorCode.CreateFailed,
                () =>
                {
                    var source = GetExternalSource(softwarePath, sourcePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source not found at '{sourcePath}'.");

                    var option = keepOnError ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;

                    // Verified against the V21 reference assembly: the overloads return
                    // IList<IEngineeringObject>, which holds PlcBlock and PlcType instances.
                    IList<IEngineeringObject> generated;

                    if (string.IsNullOrWhiteSpace(targetGroupPath))
                    {
                        generated = source.GenerateBlocksFromSource(option);
                    }
                    else
                    {
                        var target = GetPlcBlockGroupByPath(softwarePath, targetGroupPath) as PlcBlockUserGroup
                            ?? throw new PortalException(PortalErrorCode.InvalidParams,
                                $"'{targetGroupPath}' must be a block user group. Blocks cannot be generated into the 'Program blocks' system root; " +
                                "leave targetGroupPath empty to use the source's default location.");

                        generated = source.GenerateBlocksFromSource(target, option);
                    }

                    var names = new List<string>();

                    foreach (var item in generated)
                    {
                        if (item is PlcBlock block)
                        {
                            names.Add(block.Name);
                        }
                        else if (item is PlcType type)
                        {
                            names.Add(type.Name);
                        }
                        else
                        {
                            names.Add(item?.ToString() ?? string.Empty);
                        }
                    }

                    return names;
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath), ("targetGroupPath", targetGroupPath));
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
