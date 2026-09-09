using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Create, rename and delete for program blocks, PLC data types and their groups.
    ///
    /// Callers: the corresponding tools in McpServerWrite.cs, which are only registered when the
    /// server runs with '--allow-write'. Affected API: none existing - all members are new.
    /// Reads/writes no data files; these mutate the open project in memory until SaveProject.
    ///
    /// Openness constraints that shape this file:
    ///  - PlcBlockComposition has no generic Create(name) and no CreateFrom(PlcBlock), so the
    ///    only ways to make a block are CreateFB, CreateInstanceDB and XML import.
    ///  - Only user groups can be created or deleted; the system roots cannot.
    ///  - Name has a public setter on blocks, types and user groups, so rename is assignment.
    /// </summary>
    public partial class Portal
    {
        #region guards

        /// <summary>
        /// Know-how protected objects reject edits deep inside Openness with an opaque error;
        /// failing here gives the caller something actionable instead.
        /// </summary>
        private static void EnsureNotKnowHowProtected(PlcBlock block)
        {
            if (block.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Block '{block.Name}' is know-how protected. Remove the protection in TIA Portal before modifying it.");
            }
        }

        private static void EnsureNotKnowHowProtected(PlcType type)
        {
            if (type.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Type '{type.Name}' is know-how protected. Remove the protection in TIA Portal before modifying it.");
            }
        }

        private static PlcBlockUserGroup EnsureUserGroup(PlcBlockGroup? group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Block group not found at '{groupPath}'. Use 'GetSoftwareTree' to discover valid group paths.");
            }

            return group as PlcBlockUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the system group 'Program blocks', which cannot be created, renamed or deleted.");
        }

        private static PlcTypeUserGroup EnsureUserGroup(PlcTypeGroup? group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Type group not found at '{groupPath}'. Use 'GetSoftwareTree' to discover valid group paths.");
            }

            return group as PlcTypeUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the system group 'PLC data types', which cannot be created, renamed or deleted.");
        }

        private static void EnsureValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The name must not be empty.");
            }

            if (name.Contains("/"))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{name}' must be a plain name, not a path: '/' separates path segments.");
            }
        }

        #endregion

        #region groups

        /// <param name="parentGroupPath">Empty creates the group directly under the system root.</param>
        public PlcBlockUserGroup CreateBlockGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateBlockGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetPlcBlockGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteBlockGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteBlockGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    EnsureUserGroup(GetPlcBlockGroupByPath(softwarePath, groupPath), groupPath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        public PlcTypeUserGroup CreateTypeGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTypeGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetPlcTypeGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteTypeGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteTypeGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    EnsureUserGroup(GetPlcTypeGroupByPath(softwarePath, groupPath), groupPath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        #endregion

        #region blocks and types

        public bool DeleteBlock(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(DeleteBlock), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'GetBlocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(block);
                    block.Delete();

                    return true;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        public bool RenameBlock(string softwarePath, string blockPath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameBlock), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);

                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'GetBlocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(block);
                    block.Name = newName;

                    return true;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("newName", newName));
        }

        public bool DeleteType(string softwarePath, string typePath)
        {
            return Operation.Run(_logger, nameof(DeleteType), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'GetTypes' to list the available types.");

                    EnsureNotKnowHowProtected(type);
                    type.Delete();

                    return true;
                },
                ("softwarePath", softwarePath), ("typePath", typePath));
        }

        public bool RenameType(string softwarePath, string typePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameType), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);

                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'GetTypes' to list the available types.");

                    EnsureNotKnowHowProtected(type);
                    type.Name = newName;

                    return true;
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("newName", newName));
        }

        /// <summary>
        /// Creates an empty function block. Openness offers no generic "create block", so FB and
        /// instance DB are the only two kinds creatable without importing XML.
        /// </summary>
        public FB CreateFB(string softwarePath, string groupPath, string name, bool autoNumber = true, int number = 0, string language = "LAD")
        {
            return Operation.Run(_logger, nameof(CreateFB), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    if (!Enum.TryParse<ProgrammingLanguage>(language, true, out var parsedLanguage))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown programming language '{language}'. Allowed values are: {string.Join(", ", Enum.GetNames(typeof(ProgrammingLanguage)))}.");
                    }

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'.");

                    return group.Blocks.CreateFB(name, autoNumber, number, parsedLanguage);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("language", language));
        }

        /// <param name="instanceOfName">Name of the FB this instance DB is created for.</param>
        public InstanceDB CreateInstanceDB(string softwarePath, string groupPath, string name, string instanceOfName, bool autoNumber = true, int number = 0)
        {
            return Operation.Run(_logger, nameof(CreateInstanceDB), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    if (string.IsNullOrWhiteSpace(instanceOfName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "instanceOfName must name the function block this instance DB belongs to.");
                    }

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'.");

                    return group.Blocks.CreateInstanceDB(name, autoNumber, number, instanceOfName);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("instanceOfName", instanceOfName));
        }

        #endregion
    }
}
