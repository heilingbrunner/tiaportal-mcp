using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Read-only MCP tools for PLC tag tables, tags and constants.
    ///
    /// Callers: registered through Program.BuildToolTypes() and invoked directly by the tag test
    /// class. Affected API: additive only - this is a new partial of the existing McpServer type.
    /// Data: returns the ResponseTagTable*/ResponseTag*/ResponseConstant* DTOs as MCP
    /// structuredContent. Only ExportTagTable touches the file system, writing one .xml.
    ///
    /// Every tool follows the house pattern: call Portal, translate a null/empty result into a
    /// specific McpException, and wrap anything unexpected with 'when (ex is not McpException)'.
    /// </summary>
    public static partial class McpServer
    {
        #region tags

        [McpServerTool(Name = "GetTagTables", Title = "Get PLC tag tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the PLC tag tables of a plc software, optionally filtered by a regular expression on the table name")]
        public static ResponseTagTables GetTagTables(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
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

        [McpServerTool(Name = "GetTagTableInfo", Title = "Get PLC tag table info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get the details of a single PLC tag table, including its tag and constant counts")]
        public static ResponseTagTableInfo GetTagTableInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
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

        [McpServerTool(Name = "GetTags", Title = "Get PLC tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List PLC tags, either of one tag table or of every tag table of the plc software, optionally filtered by a regular expression on the tag name")]
        public static ResponseTags GetTags(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
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

        [McpServerTool(Name = "GetTagInfo", Title = "Get PLC tag info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get the details of a single PLC tag, including data type, logical address and external access flags")]
        public static ResponseTagInfo GetTagInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
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

        [McpServerTool(Name = "GetConstants", Title = "Get PLC constants", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List PLC user and/or system constants, either of one tag table or of every tag table of the plc software")]
        public static ResponseConstants GetConstants(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
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

        [McpServerTool(Name = "ExportTagTable", Title = "Export a PLC tag table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export a PLC tag table to an XML file on the file system of the machine running this server. Does not modify the project")]
        public static ResponseExportTagTable ExportTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'")] string tagTablePath,
            [Description("exportPath: directory on this machine that receives the XML file")] string exportPath,
            [Description("preservePath: recreate the tag table group structure below exportPath")] bool preservePath = false)
        {
            try
            {
                var table = Portal.ExportTagTable(softwarePath, tagTablePath, exportPath, preservePath)
                    ?? throw new McpException($"Failed exporting tag table '{tagTablePath}' from '{softwarePath}'");

                return new ResponseExportTagTable
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
    }
}
