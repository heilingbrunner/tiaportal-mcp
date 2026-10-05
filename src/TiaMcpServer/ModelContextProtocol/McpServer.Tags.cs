using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // From the former McpServer.Tags.cs:
        // Read-only MCP tools for PLC tag tables, tags and constants.
        //
        // Callers: registered through Program.BuildTools() and invoked directly by the tag test
        // class. Affected API: additive only - this is a new partial of the existing McpServer type.
        // Data: returns the ResponseTagTable*/ResponseTag*/ResponseConstant* DTOs as MCP
        // structuredContent. Only ExportXmlTagTable touches the file system, writing one .xml.
        //
        // Every tool follows the house pattern: call Portal, translate a null/empty result into a
        // specific McpException, and wrap anything unexpected with 'when (ex is not McpException)'.

        #region tags

        [McpServerTool(Name = "GetTagTables", Title = "Get tag tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the PLC tag tables of a PLC software, optionally filtered by a regular expression on the table name")]
        public static ResponseTagTables GetTagTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("regexName: optional regular expression to filter the tag table names")] string regexName = "")
        {
            try
            {
                var tables = Portal.GetTagTables(softwarePath, regexName);

                return new ResponseTagTables
                {
                    Message = $"{tables.Count} tag table(s) retrieved from '{softwarePath}'",
                    Items = tables.Select(ToTagTableInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalTagTables"] = tables.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tag tables from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetTagTableInfo", Title = "Get tag table info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get the details of a single PLC tag table, including its tag and constant counts")]
        public static ResponseTagTableInfo GetTagTableInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'")] string tagTablePath)
        {
            try
            {
                var table = Portal.GetTagTable(softwarePath, tagTablePath)
                    ?? throw new McpException($"Tag table not found at '{tagTablePath}' in '{softwarePath}'. Use 'GetTagTables' to list the available tables.");

                var info = ToTagTableInfo(table);
                info.Message = $"Tag table info retrieved from '{tagTablePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(table);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tag table info from '{tagTablePath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetTags", Title = "Get tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List PLC tags, either of one tag table or of every tag table of the PLC software, optionally filtered by a regular expression on the tag name")]
        public static ResponseTags GetTags(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: optional root-relative tag table path; empty searches every tag table")] string tagTablePath = "",
            [Description("regexName: optional regular expression to filter the tag names")] string regexName = "")
        {
            try
            {
                var tags = Portal.GetTags(softwarePath, tagTablePath, regexName);

                return new ResponseTags
                {
                    Message = $"{tags.Count} tag(s) retrieved from '{softwarePath}'",
                    Items = tags.Select(ToTagInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalTags"] = tags.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tags from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetTagInfo", Title = "Get tag info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get the details of a single PLC tag, including data type, logical address and external access flags")]
        public static ResponseTagInfo GetTagInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagPath: path of the tag including its table, e.g. 'TagGroup1/Table1/Tag_1'")] string tagPath)
        {
            try
            {
                var tag = Portal.GetTag(softwarePath, tagPath)
                    ?? throw new McpException($"Tag not found at '{tagPath}' in '{softwarePath}'. Use 'GetTags' to list the available tags.");

                var info = ToTagInfo(tag);
                info.Message = $"Tag info retrieved from '{tagPath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(tag);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tag info from '{tagPath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetConstants", Title = "Get constants", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List PLC user and/or system constants, either of one tag table or of every tag table of the PLC software")]
        public static ResponseConstants GetConstants(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: optional root-relative tag table path; empty searches every tag table")] string tagTablePath = "",
            [Description("kind: which constants to return - 'all' (default), 'user' or 'system'")] string kind = "all",
            [Description("regexName: optional regular expression to filter the constant names")] string regexName = "")
        {
            try
            {
                var normalized = (kind ?? "all").Trim().ToLowerInvariant();

                if (normalized != "all" && normalized != "user" && normalized != "system")
                {
                    throw new McpException($"Invalid kind '{kind}'. Allowed values are 'all', 'user' and 'system'.");
                }

                var items = new List<ResponseConstantInfo>();

                if (normalized == "all" || normalized == "user")
                {
                    items.AddRange(Portal.GetUserConstants(softwarePath, tagTablePath, regexName)
                        .Select(c => new ResponseConstantInfo
                        {
                            Name = c.Name,
                            Kind = "User",
                            DataTypeName = c.DataTypeName,
                            Value = c.Value?.ToString(),
                            Comment = Helper.FirstText(c.Comment)
                        }));
                }

                if (normalized == "all" || normalized == "system")
                {
                    items.AddRange(Portal.GetSystemConstants(softwarePath, tagTablePath, regexName)
                        .Select(c => new ResponseConstantInfo
                        {
                            Name = c.Name,
                            Kind = "System",
                            DataTypeName = c.DataTypeName,
                            Value = c.Value?.ToString()
                        }));
                }

                return new ResponseConstants
                {
                    Message = $"{items.Count} constant(s) retrieved from '{softwarePath}'",
                    Items = items,
                    Meta = Ok(new JsonObject { ["totalConstants"] = items.Count, ["kind"] = normalized })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving constants from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportXmlTagTable", Title = "Export tag table as XML", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export one PLC tag table as an XML file into exportPath, below its tag table group folders when preservePath is true")]
        public static ResponseExportXmlTagTable ExportXmlTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'")] string tagTablePath,
            [Description("exportPath: directory on this machine that receives the XML file")] string exportPath,
            [Description("preservePath: recreate the tag table group structure below exportPath, inside the 'PLC tags' system folder as TIA Portal names it in the current interface language")] bool preservePath = false)
        {
            try
            {
                var table = Portal.ExportXmlTagTable(softwarePath, tagTablePath, exportPath, preservePath)
                    ?? throw new McpException($"Failed exporting tag table '{tagTablePath}' from '{softwarePath}'");

                return new ResponseExportXmlTagTable
                {
                    Message = $"Tag table '{tagTablePath}' exported to '{exportPath}'",
                    Name = table.Name,
                    Path = Portal.GetTagTablePath(table),
                    Meta = Ok()
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting tag table '{tagTablePath}' from '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion

        #region mappers

        private static ResponseTagTableInfo ToTagTableInfo(PlcTagTable table) => new ResponseTagTableInfo
        {
            Name = table.Name,
            Path = Portal.GetTagTablePath(table),
            IsDefault = table.IsDefault,
            ModifiedTimeStamp = table.ModifiedTimeStamp,
            TagCount = table.Tags.Count,
            UserConstantCount = table.UserConstants.Count,
            SystemConstantCount = table.SystemConstants.Count
        };

        private static ResponseTagInfo ToTagInfo(PlcTag tag) => new ResponseTagInfo
        {
            Name = tag.Name,
            TableName = (tag.Parent as PlcTagTable)?.Name,
            Path = tag.Parent is PlcTagTable parent
                ? $"{Portal.GetTagTablePath(parent)}/{tag.Name}"
                : tag.Name,
            DataTypeName = tag.DataTypeName,
            LogicalAddress = tag.LogicalAddress,
            Comment = Helper.FirstText(tag.Comment),
            ExternalAccessible = tag.ExternalAccessible,
            ExternalVisible = tag.ExternalVisible,
            ExternalWritable = tag.ExternalWritable,
            IsSafety = tag.IsSafety
        };

        /// <summary>Standard Meta envelope, optionally merged with extra fields.</summary>
        private static JsonObject Ok(JsonObject? extra = null)
        {
            var meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = true
            };

            if (extra != null)
            {
                foreach (var pair in extra.ToList())
                {
                    meta[pair.Key] = pair.Value?.DeepClone();
                }
            }

            return meta;
        }

        #endregion

        // From the former McpServer.WatchTables.cs:
        // Read-only MCP tools for PLC watch and force tables.
        //
        // Callers: registered through Program.BuildTools() and invoked directly by the watch
        // table test class. Affected API: additive only - a new partial of the existing McpServer
        // type. Data: returns ResponseWatchTable*/TableEntryInfo as MCP structuredContent; only
        // ExportXmlWatchTable touches the file system, writing one .xml.

        #region watch and force tables

        [McpServerTool(Name = "GetWatchTables", Title = "Get watch tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the PLC watch tables of a PLC software, optionally filtered by a regular expression on the table name. Entries are omitted; use GetWatchTableInfo for one table's rows")]
        public static ResponseWatchTables GetWatchTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("regexName: optional regular expression to filter the watch table names")] string regexName = "")
        {
            try
            {
                var tables = Portal.GetWatchTables(softwarePath, regexName);

                return new ResponseWatchTables
                {
                    Message = $"{tables.Count} watch table(s) retrieved from '{softwarePath}'",
                    Items = tables.Select(t => ToWatchTableInfo(t, includeEntries: false)).ToList(),
                    Meta = Ok(new JsonObject { ["totalWatchTables"] = tables.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving watch tables from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetWatchTableInfo", Title = "Get watch table info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a single PLC watch table including all of its entries (address, display format, monitor and modify settings)")]
        public static ResponseWatchTableInfo GetWatchTableInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'")] string watchTablePath)
        {
            try
            {
                var table = Portal.GetWatchTable(softwarePath, watchTablePath)
                    ?? throw new McpException($"Watch table not found at '{watchTablePath}' in '{softwarePath}'. Use 'GetWatchTables' to list the available tables.");

                var info = ToWatchTableInfo(table, includeEntries: true);
                info.Message = $"Watch table info retrieved from '{watchTablePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(table);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving watch table info from '{watchTablePath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetForceTables", Title = "Get force tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the PLC force tables of a PLC software including their entries. A force table is created and owned by the system: it cannot be created or deleted through Openness")]
        public static ResponseWatchTables GetForceTables(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath)
        {
            try
            {
                var tables = Portal.GetForceTables(softwarePath);

                return new ResponseWatchTables
                {
                    Message = $"{tables.Count} force table(s) retrieved from '{softwarePath}'",
                    Items = tables.Select(ToForceTableInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalForceTables"] = tables.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving force tables from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportXmlWatchTable", Title = "Export watch table as XML", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export one PLC watch table as an XML file into exportPath, below its watch table group folders when preservePath is true")]
        public static ResponseExportXmlWatchTable ExportXmlWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'")] string watchTablePath,
            [Description("exportPath: directory on this machine that receives the XML file")] string exportPath,
            [Description("preservePath: recreate the watch table group structure below exportPath")] bool preservePath = false)
        {
            try
            {
                var table = Portal.ExportXmlWatchTable(softwarePath, watchTablePath, exportPath, preservePath)
                    ?? throw new McpException($"Failed exporting watch table '{watchTablePath}' from '{softwarePath}'");

                return new ResponseExportXmlWatchTable
                {
                    Message = $"Watch table '{watchTablePath}' exported to '{exportPath}'",
                    Name = table.Name,
                    Path = Portal.GetWatchTablePath(table),
                    Meta = Ok()
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting watch table '{watchTablePath}' from '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion

        #region mappers

        private static ResponseWatchTableInfo ToWatchTableInfo(PlcWatchTable table, bool includeEntries)
        {
            return new ResponseWatchTableInfo
            {
                Name = table.Name,
                Path = Portal.GetWatchTablePath(table),
                Kind = "Watch",
                IsConsistent = table.IsConsistent,
                EntryCount = table.Entries.Count,
                Entries = includeEntries ? ToEntryList(table.Entries) : null
            };
        }

        private static ResponseWatchTableInfo ToForceTableInfo(PlcForceTable table)
        {
            return new ResponseWatchTableInfo
            {
                Name = table.Name,
                Path = Portal.GetForceTablePath(table),
                Kind = "Force",
                IsConsistent = table.IsConsistent,
                EntryCount = table.Entries.Count,
                Entries = ToEntryList(table.Entries)
            };
        }

        /// <summary>
        /// Projects the shared entry composition. Watch, force and plain comment rows all live in
        /// a PlcTableCommentEntryComposition, so the concrete row type decides which fields apply.
        /// </summary>
        private static List<TableEntryInfo> ToEntryList(PlcTableCommentEntryComposition entries)
        {
            var list = new List<TableEntryInfo>();

            foreach (var entry in entries)
            {
                if (entry is PlcWatchTableEntry watch)
                {
                    list.Add(new TableEntryInfo
                    {
                        Kind = "Watch",
                        Name = watch.Name,
                        Address = watch.Address,
                        DisplayFormat = watch.DisplayFormat.ToString(),
                        MonitorTrigger = watch.MonitorTrigger.ToString(),
                        ModifyTrigger = watch.ModifyTrigger.ToString(),
                        ModifyValue = watch.ModifyValue,
                        ModifyIntention = watch.ModifyIntention.ToString()
                    });
                }
                else if (entry is PlcForceTableEntry force)
                {
                    list.Add(new TableEntryInfo
                    {
                        Kind = "Force",
                        Name = force.Name,
                        Address = force.Address,
                        DisplayFormat = force.DisplayFormat.ToString(),
                        MonitorTrigger = force.MonitorTrigger.ToString(),
                        ForceValue = force.ForceValue,
                        ForceIntention = force.ForceIntention.ToString()
                    });
                }
                else
                {
                    // A plain comment row exposes nothing but Parent and Delete, so its text is
                    // only reachable through the generic attribute bag.
                    list.Add(new TableEntryInfo
                    {
                        Kind = "Comment",
                        Name = Helper.GetAttributeList(entry)
                            .FirstOrDefault(a => a.Name == "Name")?.Value?.ToString()
                    });
                }
            }

            return list;
        }

        #endregion

        // From the former McpServerWrite.Tags.cs:
        // Write tools for PLC tag tables, tags and user constants.
        //
        // Callers: registered by Program.BuildTools() under '--allow-write'; also invoked
        // directly by the write test class. Affected API: additive - a partial of McpServer; the tools are marked [WriteTool].
        // File I/O: ImportXmlTagTable reads a caller-supplied XML file; nothing here writes files.
        //
        // There are no tools for system constants: PlcSystemConstantComposition has no Create and
        // PlcSystemConstant no setters, so they are read-only through Openness by design.

        // From the former McpServerWrite.Tables.cs:
        // Write tools for watch tables and external source files.
        //
        // Callers: registered by Program.BuildTools() under '--allow-write'; also invoked
        // directly by the write test class. Affected API: additive - a partial of McpServer; the tools are marked [WriteTool].
        // File I/O: ImportWatchTable and CreateExternalSourceFromFile read a caller-supplied file;
        // nothing here writes files.
        //
        // There are no force-table tools: PlcForceTableComposition has no Create and PlcForceTable
        // no Delete, because the force table is system-owned (one per PLC).

        #region tag tables (write)

        [WriteTool]
        [McpServerTool(Name = "CreateTagTable", Title = "Create tag table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PLC tag table in a tag table group")]
        public static ResponseCreated CreateTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative tag table group; empty creates directly below the PLC tags root")] string groupPath,
            [Description("name: name of the new tag table, without a slash")] string name)
        {
            return Guarded(nameof(CreateTagTable), () =>
            {
                Portal.CreateTagTable(softwarePath, groupPath, name);
                return Created("Tag table", name, JoinPath(groupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteTagTable", Title = "Delete tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC tag table with all of its tags and user constants. The default tag table cannot be deleted")]
        public static ResponseDeleted DeleteTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1")] string tagTablePath)
        {
            return Guarded(nameof(DeleteTagTable), () =>
            {
                Portal.DeleteTagTable(softwarePath, tagTablePath);
                return Deleted("Tag table", tagTablePath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "RenameTagTable", Title = "Rename tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC tag table")]
        public static ResponseRenamed RenameTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("newName: the new tag table name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameTagTable), () =>
            {
                Portal.RenameTagTable(softwarePath, tagTablePath, newName);
                return Renamed("Tag table", tagTablePath, newName, ReplaceLeaf(tagTablePath, newName));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateTagTableGroup", Title = "Create tag table group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the PLC tags root of the PLC software")]
        public static ResponseCreated CreateTagTableGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below PLC tags")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateTagTableGroup), () =>
            {
                Portal.CreateTagTableGroup(softwarePath, parentGroupPath, name);
                return Created("Tag table group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteTagTableGroup", Title = "Delete tag table group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a tag table group and everything inside it. The PLC tags system group itself cannot be deleted")]
        public static ResponseDeleted DeleteTagTableGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteTagTableGroup), () =>
            {
                Portal.DeleteTagTableGroup(softwarePath, groupPath);
                return Deleted("Tag table group", groupPath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "ImportXmlTagTable", Title = "Import tag table from XML", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import a PLC tag table from an XML file into a tag table group. An existing table of the same name is replaced unless overwrite is false")]
        public static ResponseImported ImportXmlTagTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative tag table group that receives the table; empty uses the PLC tags root. A leading 'PLC tags' segment, as written by preservePath exports, is accepted and ignored")] string groupPath,
            [Description("importPath: full path of the XML file to import")] string importPath,
            [Description("overwrite: replace an existing tag table of the same name (default true)")] bool overwrite = true)
        {
            return Guarded(nameof(ImportXmlTagTable), () =>
            {
                Portal.ImportXmlTagTable(softwarePath, groupPath, importPath, overwrite);
                return Imported("Tag table", groupPath, importPath);
            });
        }

        #endregion

        #region tags (write)

        [WriteTool]
        [McpServerTool(Name = "CreateTag", Title = "Create tag", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PLC tag in a tag table")]
        public static ResponseCreated CreateTag(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1")] string tagTablePath,
            [Description("name: name of the new tag, without a slash")] string name,
            [Description("dataTypeName: PLC data type such as Bool, Int or Word; empty creates the tag with its default type")] string dataTypeName = "",
            [Description("logicalAddress: absolute address such as %I0.0, %QW4 or %M10.1")] string logicalAddress = "")
        {
            return Guarded(nameof(CreateTag), () =>
            {
                Portal.CreateTag(softwarePath, tagTablePath, name, dataTypeName, logicalAddress);
                return Created("Tag", name, JoinPath(tagTablePath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "UpdateTag", Title = "Update tag", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Change one or more properties of an existing PLC tag. Every argument left empty or null keeps the current value")]
        public static ResponseRenamed UpdateTag(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
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

        [WriteTool]
        [McpServerTool(Name = "DeleteTag", Title = "Delete tag", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC tag from its tag table")]
        public static ResponseDeleted DeleteTag(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1")] string tagPath)
        {
            return Guarded(nameof(DeleteTag), () =>
            {
                Portal.DeleteTag(softwarePath, tagPath);
                return Deleted("Tag", tagPath);
            });
        }

        #endregion

        #region user constants (write)

        [WriteTool]
        [McpServerTool(Name = "CreateUserConstant", Title = "Create user constant", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a user constant in a tag table. System constants are read-only and cannot be created")]
        public static ResponseCreated CreateUserConstant(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("name: name of the new constant, without a slash")] string name,
            [Description("dataTypeName: PLC data type such as Int or Real; empty creates the constant with its default type")] string dataTypeName = "",
            [Description("value: the constant value as text, e.g. 42 or 3.14")] string value = "")
        {
            return Guarded(nameof(CreateUserConstant), () =>
            {
                Portal.CreateUserConstant(softwarePath, tagTablePath, name, dataTypeName, value);
                return Created("User constant", name, JoinPath(tagTablePath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "UpdateUserConstant", Title = "Update user constant", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Change the name, data type or value of an existing user constant. Every argument left null keeps the current value")]
        public static ResponseRenamed UpdateUserConstant(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("name: current name of the constant")] string name,
            [Description("newName: optional new constant name, without a slash")] string? newName = null,
            [Description("dataTypeName: optional new PLC data type")] string? dataTypeName = null,
            [Description("value: optional new value as text")] string? value = null)
        {
            return Guarded(nameof(UpdateUserConstant), () =>
            {
                Portal.UpdateUserConstant(softwarePath, tagTablePath, name, newName, dataTypeName, value);

                var oldPath = JoinPath(tagTablePath, name);

                return new ResponseRenamed
                {
                    Kind = "User constant",
                    OldPath = oldPath,
                    NewName = newName,
                    NewPath = newName == null ? oldPath : JoinPath(tagTablePath, newName),
                    Message = $"User constant '{oldPath}' updated. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteUserConstant", Title = "Delete user constant", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a user constant from a tag table. System constants cannot be deleted")]
        public static ResponseDeleted DeleteUserConstant(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table")] string tagTablePath,
            [Description("name: name of the constant to delete")] string name)
        {
            return Guarded(nameof(DeleteUserConstant), () =>
            {
                Portal.DeleteUserConstant(softwarePath, tagTablePath, name);
                return Deleted("User constant", JoinPath(tagTablePath, name));
            });
        }

        #endregion

        #region watch tables (write)

        [WriteTool]
        [McpServerTool(Name = "CreateWatchTable", Title = "Create watch table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PLC watch table. Force tables cannot be created: the system owns the single force table per PLC")]
        public static ResponseCreated CreateWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative watch table group; empty creates directly below the Watch and force tables root")] string groupPath,
            [Description("name: name of the new watch table, without a slash")] string name)
        {
            return Guarded(nameof(CreateWatchTable), () =>
            {
                Portal.CreateWatchTable(softwarePath, groupPath, name);
                return Created("Watch table", name, JoinPath(groupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "RenameWatchTable", Title = "Rename watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC watch table")]
        public static ResponseRenamed RenameWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table")] string watchTablePath,
            [Description("newName: the new watch table name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameWatchTable), () =>
            {
                Portal.RenameWatchTable(softwarePath, watchTablePath, newName);
                return Renamed("Watch table", watchTablePath, newName, ReplaceLeaf(watchTablePath, newName));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteWatchTable", Title = "Delete watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC watch table with all of its entries. Force tables cannot be deleted")]
        public static ResponseDeleted DeleteWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table")] string watchTablePath)
        {
            return Guarded(nameof(DeleteWatchTable), () =>
            {
                Portal.DeleteWatchTable(softwarePath, watchTablePath);
                return Deleted("Watch table", watchTablePath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateWatchTableGroup", Title = "Create watch table group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the Watch and force tables root of the PLC software")]
        public static ResponseCreated CreateWatchTableGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below the root")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateWatchTableGroup), () =>
            {
                Portal.CreateWatchTableGroup(softwarePath, parentGroupPath, name);
                return Created("Watch table group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteWatchTableGroup", Title = "Delete watch table group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a watch table group and everything inside it. The Watch and force tables system group itself cannot be deleted")]
        public static ResponseDeleted DeleteWatchTableGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteWatchTableGroup), () =>
            {
                Portal.DeleteWatchTableGroup(softwarePath, groupPath);
                return Deleted("Watch table group", groupPath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "ImportWatchTable", Title = "Import watch table from XML", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import a PLC watch table from an XML file into a watch table group. An existing table of the same name is replaced unless overwrite is false")]
        public static ResponseImported ImportWatchTable(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
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
    }
}
