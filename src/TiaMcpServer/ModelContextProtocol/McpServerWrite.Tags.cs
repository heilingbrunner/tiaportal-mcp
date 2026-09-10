using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Write tools for PLC tag tables, tags and user constants.
    ///
    /// Callers: registered by Program.BuildToolTypes() under '--allow-write'; also invoked
    /// directly by the write test class. Affected API: additive - a partial of McpServerWrite.
    /// File I/O: ImportTagTable reads a caller-supplied XML file; nothing here writes files.
    ///
    /// There are no tools for system constants: PlcSystemConstantComposition has no Create and
    /// PlcSystemConstant no setters, so they are read-only through Openness by design.
    /// </summary>
    public static partial class McpServerWrite
    {
        #region tag tables

        [McpServerTool(Name = "CreateTagTable", Title = "Create a PLC tag table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PLC tag table in a tag table group")]
        public static ResponseCreated CreateTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative tag table group; empty creates directly below the PLC tags root")] string groupPath,
            [Description("name: name of the new tag table, without a slash")] string name)
        {
            return Guarded(nameof(CreateTagTable), () =>
            {
                Portal.CreateTagTable(softwarePath, groupPath, name);
                return Created("Tag table", name, Join(groupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteTagTable", Title = "Delete a PLC tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC tag table with all of its tags and user constants. The default tag table cannot be deleted")]
        public static ResponseDeleted DeleteTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1")] string tagTablePath)
        {
            return Guarded(nameof(DeleteTagTable), () =>
            {
                Portal.DeleteTagTable(softwarePath, tagTablePath);
                return Deleted("Tag table", tagTablePath);
            });
        }

        [McpServerTool(Name = "RenameTagTable", Title = "Rename a PLC tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC tag table")]
        public static ResponseRenamed RenameTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("newName: the new tag table name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameTagTable), () =>
            {
                Portal.RenameTagTable(softwarePath, tagTablePath, newName);
                return Renamed("Tag table", tagTablePath, newName, ReplaceLeaf(tagTablePath, newName));
            });
        }

        [McpServerTool(Name = "CreateTagTableGroup", Title = "Create a tag table group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the PLC tags root of the plc software")]
        public static ResponseCreated CreateTagTableGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below PLC tags")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateTagTableGroup), () =>
            {
                Portal.CreateTagTableGroup(softwarePath, parentGroupPath, name);
                return Created("Tag table group", name, Join(parentGroupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteTagTableGroup", Title = "Delete a tag table group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a tag table group and everything inside it. The PLC tags system group itself cannot be deleted")]
        public static ResponseDeleted DeleteTagTableGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteTagTableGroup), () =>
            {
                Portal.DeleteTagTableGroup(softwarePath, groupPath);
                return Deleted("Tag table group", groupPath);
            });
        }

        [McpServerTool(Name = "ImportTagTable", Title = "Import a PLC tag table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import a PLC tag table from an XML file on the file system of the machine running this server")]
        public static ResponseImported ImportTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative tag table group that receives the table; empty uses the PLC tags root")] string groupPath,
            [Description("importPath: full path of the XML file to import")] string importPath,
            [Description("overwrite: replace an existing tag table of the same name (default true)")] bool overwrite = true)
        {
            return Guarded(nameof(ImportTagTable), () =>
            {
                Portal.ImportTagTable(softwarePath, groupPath, importPath, overwrite);
                return Imported("Tag table", groupPath, importPath);
            });
        }

        #endregion

        #region tags

        [McpServerTool(Name = "CreateTag", Title = "Create a PLC tag", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PLC tag in a tag table")]
        public static ResponseCreated CreateTag(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1")] string tagTablePath,
            [Description("name: name of the new tag, without a slash")] string name,
            [Description("dataTypeName: PLC data type such as Bool, Int or Word; empty creates the tag with its default type")] string dataTypeName = "",
            [Description("logicalAddress: absolute address such as %I0.0, %QW4 or %M10.1")] string logicalAddress = "")
        {
            return Guarded(nameof(CreateTag), () =>
            {
                Portal.CreateTag(softwarePath, tagTablePath, name, dataTypeName, logicalAddress);
                return Created("Tag", name, Join(tagTablePath, name));
            });
        }

        [McpServerTool(Name = "UpdateTag", Title = "Update a PLC tag", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Change one or more properties of an existing PLC tag. Every argument left empty or null keeps the current value")]
        public static ResponseRenamed UpdateTag(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1")] string tagPath,
            [Description("newName: optional new tag name, without a slash")] string? newName = null,
            [Description("dataTypeName: optional new PLC data type")] string? dataTypeName = null,
            [Description("logicalAddress: optional new absolute address")] string? logicalAddress = null,
            [Description("externalAccessible: optional new value for accessibility from HMI/OPC UA")] bool? externalAccessible = null,
            [Description("externalVisible: optional new value for visibility in HMI/OPC UA")] bool? externalVisible = null,
            [Description("externalWritable: optional new value for writability from HMI/OPC UA")] bool? externalWritable = null)
        {
            return Guarded(nameof(UpdateTag), () =>
            {
                Portal.UpdateTag(softwarePath, tagPath, newName, dataTypeName, logicalAddress,
                                 externalAccessible, externalVisible, externalWritable);

                var newPath = newName == null ? tagPath : ReplaceLeaf(tagPath, newName);

                return new ResponseRenamed
                {
                    Kind = "Tag",
                    OldPath = tagPath,
                    NewName = newName,
                    NewPath = newPath,
                    Message = $"Tag '{tagPath}' updated. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [McpServerTool(Name = "DeleteTag", Title = "Delete a PLC tag", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC tag from its tag table")]
        public static ResponseDeleted DeleteTag(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1")] string tagPath)
        {
            return Guarded(nameof(DeleteTag), () =>
            {
                Portal.DeleteTag(softwarePath, tagPath);
                return Deleted("Tag", tagPath);
            });
        }

        #endregion

        #region user constants

        [McpServerTool(Name = "CreateUserConstant", Title = "Create a PLC user constant", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a user constant in a tag table. System constants are read-only and cannot be created")]
        public static ResponseCreated CreateUserConstant(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("name: name of the new constant, without a slash")] string name,
            [Description("dataTypeName: PLC data type such as Int or Real; empty creates the constant with its default type")] string dataTypeName = "",
            [Description("value: the constant value as text, e.g. 42 or 3.14")] string value = "")
        {
            return Guarded(nameof(CreateUserConstant), () =>
            {
                Portal.CreateUserConstant(softwarePath, tagTablePath, name, dataTypeName, value);
                return Created("User constant", name, Join(tagTablePath, name));
            });
        }

        [McpServerTool(Name = "UpdateUserConstant", Title = "Update a PLC user constant", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Change the name, data type or value of an existing user constant. Every argument left null keeps the current value")]
        public static ResponseRenamed UpdateUserConstant(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("name: current name of the constant")] string name,
            [Description("newName: optional new constant name, without a slash")] string? newName = null,
            [Description("dataTypeName: optional new PLC data type")] string? dataTypeName = null,
            [Description("value: optional new value as text")] string? value = null)
        {
            return Guarded(nameof(UpdateUserConstant), () =>
            {
                Portal.UpdateUserConstant(softwarePath, tagTablePath, name, newName, dataTypeName, value);

                var oldPath = Join(tagTablePath, name);

                return new ResponseRenamed
                {
                    Kind = "User constant",
                    OldPath = oldPath,
                    NewName = newName,
                    NewPath = newName == null ? oldPath : Join(tagTablePath, newName),
                    Message = $"User constant '{oldPath}' updated. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [McpServerTool(Name = "DeleteUserConstant", Title = "Delete a PLC user constant", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a user constant from a tag table. System constants cannot be deleted")]
        public static ResponseDeleted DeleteUserConstant(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("name: name of the constant to delete")] string name)
        {
            return Guarded(nameof(DeleteUserConstant), () =>
            {
                Portal.DeleteUserConstant(softwarePath, tagTablePath, name);
                return Deleted("User constant", Join(tagTablePath, name));
            });
        }

        #endregion
    }
}
