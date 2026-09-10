using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Write tools for program blocks, PLC data types and their groups.
    ///
    /// Callers: registered by Program.BuildToolTypes() under '--allow-write'; also invoked
    /// directly by the write test class. Affected API: additive - a partial of McpServerWrite.
    /// No file I/O; changes are in memory until SaveProject / SaveSession.
    ///
    /// Openness offers no generic "create block" and no move/copy, so the creation surface here
    /// is CreateFB and CreateInstanceDB only - anything else arrives through ImportBlock.
    /// </summary>
    public static partial class McpServerWrite
    {
        #region groups

        [McpServerTool(Name = "CreateBlockGroup", Title = "Create a block group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the Program blocks root of the plc software")]
        public static ResponseCreated CreateBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below Program blocks")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateBlockGroup), () =>
            {
                Portal.CreateBlockGroup(softwarePath, parentGroupPath, name);
                return Created("Block group", name, Join(parentGroupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteBlockGroup", Title = "Delete a block group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a block group and everything inside it. The Program blocks system group itself cannot be deleted")]
        public static ResponseDeleted DeleteBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete, e.g. Common/CarrierRegister")] string groupPath)
        {
            return Guarded(nameof(DeleteBlockGroup), () =>
            {
                Portal.DeleteBlockGroup(softwarePath, groupPath);
                return Deleted("Block group", groupPath);
            });
        }

        [McpServerTool(Name = "CreateTypeGroup", Title = "Create a PLC data type group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the PLC data types root of the plc software")]
        public static ResponseCreated CreateTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below PLC data types")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateTypeGroup), () =>
            {
                Portal.CreateTypeGroup(softwarePath, parentGroupPath, name);
                return Created("Type group", name, Join(parentGroupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteTypeGroup", Title = "Delete a PLC data type group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC data type group and everything inside it. The PLC data types system group itself cannot be deleted")]
        public static ResponseDeleted DeleteTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteTypeGroup), () =>
            {
                Portal.DeleteTypeGroup(softwarePath, groupPath);
                return Deleted("Type group", groupPath);
            });
        }

        #endregion

        #region blocks and types

        [McpServerTool(Name = "DeleteBlock", Title = "Delete a block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a program block. Know-how protected blocks are rejected: remove the protection in TIA Portal first")]
        public static ResponseDeleted DeleteBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1")] string blockPath)
        {
            return Guarded(nameof(DeleteBlock), () =>
            {
                Portal.DeleteBlock(softwarePath, blockPath);
                return Deleted("Block", blockPath);
            });
        }

        [McpServerTool(Name = "RenameBlock", Title = "Rename a block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a program block. Know-how protected blocks are rejected")]
        public static ResponseRenamed RenameBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("newName: the new block name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameBlock), () =>
            {
                Portal.RenameBlock(softwarePath, blockPath, newName);
                return Renamed("Block", blockPath, newName, ReplaceLeaf(blockPath, newName));
            });
        }

        [McpServerTool(Name = "DeleteType", Title = "Delete a PLC data type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC data type (UDT). Know-how protected types are rejected")]
        public static ResponseDeleted DeleteType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type, e.g. Common/CarrierRegister/ML_SubstratState")] string typePath)
        {
            return Guarded(nameof(DeleteType), () =>
            {
                Portal.DeleteType(softwarePath, typePath);
                return Deleted("Type", typePath);
            });
        }

        [McpServerTool(Name = "RenameType", Title = "Rename a PLC data type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC data type (UDT). Know-how protected types are rejected")]
        public static ResponseRenamed RenameType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type")] string typePath,
            [Description("newName: the new type name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameType), () =>
            {
                Portal.RenameType(softwarePath, typePath, newName);
                return Renamed("Type", typePath, newName, ReplaceLeaf(typePath, newName));
            });
        }

        [McpServerTool(Name = "CreateFB", Title = "Create a function block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an empty function block. Openness has no generic create-block operation: FB and instance DB are the only kinds creatable without importing XML")]
        public static ResponseCreated CreateFB(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the FB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new function block, without a slash")] string name,
            [Description("language: programming language such as LAD (default), FBD, STL, SCL or GRAPH")] string language = "LAD",
            [Description("autoNumber: let TIA Portal assign the block number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number, used only when autoNumber is false")] int number = 0)
        {
            return Guarded(nameof(CreateFB), () =>
            {
                var block = Portal.CreateFB(softwarePath, groupPath, name, autoNumber, number, language);
                return Created("FB", block.Name, Join(groupPath, block.Name));
            });
        }

        [McpServerTool(Name = "CreateInstanceDB", Title = "Create an instance data block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an instance data block for an existing function block")]
        public static ResponseCreated CreateInstanceDB(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the DB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new instance data block, without a slash")] string name,
            [Description("instanceOfName: name of the function block this instance DB belongs to")] string instanceOfName,
            [Description("autoNumber: let TIA Portal assign the block number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number, used only when autoNumber is false")] int number = 0)
        {
            return Guarded(nameof(CreateInstanceDB), () =>
            {
                var block = Portal.CreateInstanceDB(softwarePath, groupPath, name, instanceOfName, autoNumber, number);
                return Created("InstanceDB", block.Name, Join(groupPath, block.Name));
            });
        }

        #endregion
    }
}
