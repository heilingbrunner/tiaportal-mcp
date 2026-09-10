using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Write tools for watch tables and external source files.
    ///
    /// Callers: registered by Program.BuildToolTypes() under '--allow-write'; also invoked
    /// directly by the write test class. Affected API: additive - a partial of McpServerWrite.
    /// File I/O: ImportWatchTable and CreateExternalSourceFromFile read a caller-supplied file;
    /// nothing here writes files.
    ///
    /// There are no force-table tools: PlcForceTableComposition has no Create and PlcForceTable
    /// no Delete, because the force table is system-owned (one per PLC).
    /// </summary>
    public static partial class McpServerWrite
    {
        #region watch tables

        [McpServerTool(Name = "CreateWatchTable", Title = "Create a watch table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PLC watch table. Force tables cannot be created: the system owns the single force table per PLC")]
        public static ResponseCreated CreateWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative watch table group; empty creates directly below the Watch and force tables root")] string groupPath,
            [Description("name: name of the new watch table, without a slash")] string name)
        {
            return Guarded(nameof(CreateWatchTable), () =>
            {
                Portal.CreateWatchTable(softwarePath, groupPath, name);
                return Created("Watch table", name, Join(groupPath, name));
            });
        }

        [McpServerTool(Name = "RenameWatchTable", Title = "Rename a watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC watch table")]
        public static ResponseRenamed RenameWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table")] string watchTablePath,
            [Description("newName: the new watch table name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameWatchTable), () =>
            {
                Portal.RenameWatchTable(softwarePath, watchTablePath, newName);
                return Renamed("Watch table", watchTablePath, newName, ReplaceLeaf(watchTablePath, newName));
            });
        }

        [McpServerTool(Name = "DeleteWatchTable", Title = "Delete a watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC watch table with all of its entries. Force tables cannot be deleted")]
        public static ResponseDeleted DeleteWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table")] string watchTablePath)
        {
            return Guarded(nameof(DeleteWatchTable), () =>
            {
                Portal.DeleteWatchTable(softwarePath, watchTablePath);
                return Deleted("Watch table", watchTablePath);
            });
        }

        [McpServerTool(Name = "CreateWatchTableGroup", Title = "Create a watch table group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the Watch and force tables root of the plc software")]
        public static ResponseCreated CreateWatchTableGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below the root")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateWatchTableGroup), () =>
            {
                Portal.CreateWatchTableGroup(softwarePath, parentGroupPath, name);
                return Created("Watch table group", name, Join(parentGroupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteWatchTableGroup", Title = "Delete a watch table group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a watch table group and everything inside it. The Watch and force tables system group itself cannot be deleted")]
        public static ResponseDeleted DeleteWatchTableGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteWatchTableGroup), () =>
            {
                Portal.DeleteWatchTableGroup(softwarePath, groupPath);
                return Deleted("Watch table group", groupPath);
            });
        }

        [McpServerTool(Name = "ImportWatchTable", Title = "Import a watch table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import a PLC watch table from an XML file on the file system of the machine running this server")]
        public static ResponseImported ImportWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative watch table group that receives the table; empty uses the root")] string groupPath,
            [Description("importPath: full path of the XML file to import")] string importPath,
            [Description("overwrite: replace an existing watch table of the same name (default true)")] bool overwrite = true)
        {
            return Guarded(nameof(ImportWatchTable), () =>
            {
                Portal.ImportWatchTable(softwarePath, groupPath, importPath, overwrite);
                return Imported("Watch table", groupPath, importPath);
            });
        }

        #endregion

        #region external sources

        [McpServerTool(Name = "CreateExternalSourceFromFile", Title = "Add an external source file", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Add a source file (for example an SCL file) from the file system into the external source files of the plc software")]
        public static ResponseCreated CreateExternalSourceFromFile(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative external source group; empty uses the External source files root")] string groupPath,
            [Description("name: name the source gets in the project, without a slash")] string name,
            [Description("filePath: full path of the source file on the machine running this server")] string filePath)
        {
            return Guarded(nameof(CreateExternalSourceFromFile), () =>
            {
                Portal.CreateExternalSourceFromFile(softwarePath, groupPath, name, filePath);
                return Created("External source", name, Join(groupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteExternalSource", Title = "Delete an external source file", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Remove an external source file from the plc software")]
        public static ResponseDeleted DeleteExternalSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1")] string sourcePath)
        {
            return Guarded(nameof(DeleteExternalSource), () =>
            {
                Portal.DeleteExternalSource(softwarePath, sourcePath);
                return Deleted("External source", sourcePath);
            });
        }

        [McpServerTool(Name = "CreateExternalSourceGroup", Title = "Create an external source group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the External source files root of the plc software")]
        public static ResponseCreated CreateExternalSourceGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below the root")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateExternalSourceGroup), () =>
            {
                Portal.CreateExternalSourceGroup(softwarePath, parentGroupPath, name);
                return Created("External source group", name, Join(parentGroupPath, name));
            });
        }

        [McpServerTool(Name = "DeleteExternalSourceGroup", Title = "Delete an external source group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete an external source group and everything inside it. The External source files system group itself cannot be deleted")]
        public static ResponseDeleted DeleteExternalSourceGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteExternalSourceGroup), () =>
            {
                Portal.DeleteExternalSourceGroup(softwarePath, groupPath);
                return Deleted("External source group", groupPath);
            });
        }

        [McpServerTool(Name = "GenerateBlocksFromSource", Title = "Generate blocks from an external source", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile an external source file into program blocks and PLC data types. A target must be a block user group: blocks cannot be generated into the Program blocks root")]
        public static ResponseGenerateBlocks GenerateBlocksFromSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1")] string sourcePath,
            [Description("targetGroupPath: optional root-relative block user group that receives the blocks; empty uses the source default location")] string targetGroupPath = "",
            [Description("keepOnError: keep successfully generated blocks even when others fail (default false)")] bool keepOnError = false)
        {
            return Guarded(nameof(GenerateBlocksFromSource), () =>
            {
                var names = Portal.GenerateBlocksFromSource(softwarePath, sourcePath, targetGroupPath, keepOnError);

                return new ResponseGenerateBlocks
                {
                    GeneratedNames = names,
                    Count = names.Count,
                    Message = $"{names.Count} object(s) generated from '{sourcePath}'. {SaveHint}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["pendingSave"] = true,
                        ["generatedCount"] = names.Count,
                        ["keepOnError"] = keepOnError
                    }
                };
            });
        }

        #endregion
    }
}
